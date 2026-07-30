using System;
using System.Collections;
using MirraGames.SDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[DefaultExecutionOrder(-9000)]
public sealed class OfflineRewardManager : MonoBehaviour
{
    private const float DependencyTimeoutSeconds = 20f;
    private const float HeartbeatIntervalSeconds = 30f;
    private const string WindowResourcePath = "OfflineReward";
    private const string MonthlyBoostUntilKey = "OfflineReward.MonthlyBoostUntilUnix";

    private static OfflineRewardManager _instance;

    private OfflineRewardWindow _window;
    private Coroutine _pendingEvaluation;
    private bool _initialized;
    private bool _claimPending;
    private bool _isBackgrounded;
    private long _pausedAtUnix;
    private long _evaluatedAwaySeconds;
    private string _evaluationSource = "session_start";

    public static OfflineRewardManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(OfflineRewardManager));
                _instance = go.AddComponent<OfflineRewardManager>();
            }

            return _instance;
        }
    }

    public bool IsWindowOpen => _window != null && _window.IsVisible;
    public bool IsMonthlyBoostActive => GetMonthlyBoostRemainingSeconds() > 0L;
    public int CurrentClaimMultiplier => IsMonthlyBoostActive
        ? OfflineRewardRules.MonthlyBoostMultiplier
        : 1;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        yield return WaitForSaveAndCurrency();

        long now = TrustedUtcNowUnix();
        long previous = G.Save != null ? G.Save.LoadOfflineRewardLastSeenUnix() : 0L;
        PersistLastSeen(now);
        _initialized = true;

        StartCoroutine(HeartbeatRoutine());

        if (previous <= 0L || previous > now)
            yield break;

        long elapsed = Math.Max(0L, now - previous);
        yield return WaitForPlayableScene();
        EvaluateAndShow(elapsed, "session_start");
    }

    private void OnApplicationPause(bool paused)
    {
        SetBackgrounded(paused);
    }

    private void OnApplicationFocus(bool focused)
    {
        SetBackgrounded(!focused);
    }

    private void SetBackgrounded(bool backgrounded)
    {
        if (_isBackgrounded == backgrounded)
            return;

        _isBackgrounded = backgrounded;
        long now = TrustedUtcNowUnix();
        if (backgrounded)
        {
            _pausedAtUnix = now;
            PersistLastSeen(now);
            return;
        }

        if (_pausedAtUnix > 0L)
        {
            long elapsed = Math.Max(0L, now - _pausedAtUnix);
            _pausedAtUnix = 0L;
            ScheduleEvaluation(elapsed, "application_resume");
        }

        PersistLastSeen(now);
    }

    private void OnApplicationQuit()
    {
        PersistLastSeen(TrustedUtcNowUnix());
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        if (_initialized)
            PersistLastSeen(TrustedUtcNowUnix());
        UnsubscribeFromShop();
        _instance = null;
    }

    public void ClaimNormal(string source)
    {
        int multiplier = CurrentClaimMultiplier;
        Claim(
            multiplier,
            0,
            source,
            multiplier == OfflineRewardRules.MonthlyBoostMultiplier
                ? "x20_monthly"
                : source == "close_button" ? "close_as_normal" : "normal");
    }

    public void ClaimBoosted()
    {
        ClaimWithGems();
    }

    public void ClaimWithAd()
    {
        if (_claimPending || _window == null)
            return;

        if (IsMonthlyBoostActive)
        {
            ClaimNormal("monthly_collect");
            return;
        }

        RefreshIncomeFromClock();
        OfflineRewardSnapshot preview = BuildSnapshot(_evaluatedAwaySeconds);
        if (G.Ad == null)
        {
            TrackClaim(
                false,
                OfflineRewardRules.AdMultiplier,
                0,
                "x5_ad_button",
                "x5_ad",
                preview,
                0d,
                "ads_unavailable");
            G.Sound?.Play(GameAudioId.SFX_CURRENCY_FAIL);
            return;
        }

        _claimPending = true;
        _window.SetActionsInteractable(false);
        G.Ad.ShowRewardedAd("OfflineRewardX5", success =>
        {
            _claimPending = false;
            if (!success)
            {
                _window?.SetActionsInteractable(true);
                TrackClaim(
                    false,
                    OfflineRewardRules.AdMultiplier,
                    0,
                    "x5_ad_button",
                    "x5_ad",
                    preview,
                    0d,
                    "ad_failed_or_closed");
                return;
            }

            Claim(
                OfflineRewardRules.AdMultiplier,
                0,
                "x5_ad_button",
                "x5_ad");
        });
    }

    public void ClaimWithGems()
    {
        if (IsMonthlyBoostActive)
        {
            ClaimNormal("monthly_collect");
            return;
        }

        Claim(
            OfflineRewardRules.BoostMultiplier,
            OfflineRewardRules.BoostPriceGems,
            "x10_button",
            "x10_gems");
    }

    public void PurchaseMonthlyBoost()
    {
        if (_claimPending || _window == null)
            return;

        if (IsMonthlyBoostActive)
        {
            _window.Refresh(BuildSnapshot(_evaluatedAwaySeconds));
            return;
        }

        if (G.Currency == null ||
            !G.Currency.RemoveCurrency(
                CurrencyType.Gems,
                OfflineRewardRules.MonthlyBoostPriceGems,
                "offline_reward_x20_monthly"))
        {
            OpenCurrencyShopAndReturn();
            return;
        }

        long now = TrustedUtcNowUnix();
        long currentUntil = LoadMonthlyBoostUntil();
        long until = Math.Max(now, currentUntil) +
                     OfflineRewardRules.MonthlyBoostDurationDays * 86400L;
        PlayerPrefs.SetString(
            MonthlyBoostUntilKey,
            until.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();

        G.Sound?.Play(GameAudioId.SFX_SHOP_PURCHASE_MAJOR);
        _window.Refresh(BuildSnapshot(_evaluatedAwaySeconds));
        GameAnalytics.TrackCritical(
            AnalyticsEventNames.PurchaseResult,
            GameAnalytics.Params(
                "product_id", "offline_x20_30d",
                "source", "offline_reward_window",
                "currency_type", "gems",
                "price", OfflineRewardRules.MonthlyBoostPriceGems,
                "duration_days", OfflineRewardRules.MonthlyBoostDurationDays,
                "multiplier", OfflineRewardRules.MonthlyBoostMultiplier,
                "result", "success"),
            "offline_x20_30d:" + until);
    }

    private IEnumerator WaitForSaveAndCurrency()
    {
        float startedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedAt < DependencyTimeoutSeconds)
        {
            if (G.Save != null && G.Save.IsReady && G.Currency != null && G.Currency.IsInitialized)
                yield break;

            yield return null;
        }

        Debug.LogWarning("[OfflineReward] Save or currency manager was not ready before the timeout.");
    }

    private static IEnumerator WaitForPlayableScene()
    {
        float startedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedAt < DependencyTimeoutSeconds)
        {
            bool gameplayReady = AnalyticsManager.Current != null &&
                                 AnalyticsManager.Current.IsSessionStarted;
            bool notLoadingScene = !string.Equals(
                SceneManager.GetActiveScene().name,
                "LoadingScene",
                StringComparison.OrdinalIgnoreCase);
            if (gameplayReady && notLoadingScene)
            {
                yield return null;
                yield return null;
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator HeartbeatRoutine()
    {
        var wait = new WaitForSecondsRealtime(HeartbeatIntervalSeconds);
        while (true)
        {
            yield return wait;
            if (!_isBackgrounded)
                PersistLastSeen(TrustedUtcNowUnix());
        }
    }

    private void ScheduleEvaluation(long elapsedSeconds, string source)
    {
        if (_pendingEvaluation != null)
            StopCoroutine(_pendingEvaluation);
        _pendingEvaluation = StartCoroutine(EvaluateAfterResume(elapsedSeconds, source));
    }

    private IEnumerator EvaluateAfterResume(long elapsedSeconds, string source)
    {
        yield return null;
        yield return null;
        _pendingEvaluation = null;
        EvaluateAndShow(elapsedSeconds, source);
    }

    private void EvaluateAndShow(long elapsedSeconds, string source)
    {
        if (elapsedSeconds < OfflineRewardRules.MinimumAwaySeconds || IsWindowOpen)
            return;
        if (G.Currency == null || !G.Currency.IsInitialized)
            return;

        RefreshIncomeFromClock();
        OfflineRewardSnapshot snapshot = BuildSnapshot(elapsedSeconds);
        if (!OfflineRewardRules.ShouldPresent(elapsedSeconds, snapshot.BaseIncome))
            return;

        _evaluatedAwaySeconds = elapsedSeconds;
        _evaluationSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
        ShowWindow(snapshot);

        GameAnalytics.Track(AnalyticsEventNames.OfflineRewardPresented, GameAnalytics.Params(
            "away_duration_sec", elapsedSeconds,
            "credited_duration_sec", snapshot.CappedElapsedSeconds,
            "max_accrual_sec", OfflineRewardRules.MaxAccrualSeconds,
            "base_income", snapshot.BaseIncome,
            "displayed_reward", snapshot.DisplayedReward,
            "income_source_count", snapshot.SourceCount,
            "ad_multiplier", OfflineRewardRules.AdMultiplier,
            "gems_multiplier", OfflineRewardRules.BoostMultiplier,
            "boost_price_gems", OfflineRewardRules.BoostPriceGems,
            "monthly_boost_active", IsMonthlyBoostActive,
            "monthly_boost_multiplier", OfflineRewardRules.MonthlyBoostMultiplier,
            "monthly_boost_remaining_sec", GetMonthlyBoostRemainingSeconds(),
            "source", _evaluationSource,
            "result", "success"),
            AnalyticsPriority.Normal,
            "offline_reward:" + _evaluationSource);
    }

    private void ShowWindow(OfflineRewardSnapshot snapshot)
    {
        if (_window != null)
        {
            _window.Refresh(snapshot);
            _window.SetVisible(true);
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(WindowResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[OfflineReward] Resources/{WindowResourcePath}.prefab was not found.");
            return;
        }

        var canvasObject = new GameObject(
            "OfflineRewardCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20000;

        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject windowObject = Instantiate(prefab, canvasObject.transform, false);
        windowObject.name = "OfflineRewardWindow";
        _window = windowObject.GetComponent<OfflineRewardWindow>();
        if (_window == null)
            _window = windowObject.AddComponent<OfflineRewardWindow>();
        _window.Initialize(this, snapshot);

        if (G.Control != null)
            G.Control.CursorActive = true;
        G.Input?.AOpenWindow?.Invoke(_window);
        G.Sound?.Play(GameAudioId.SFX_REWARD_READY);
    }

    private void Claim(
        int multiplier,
        int gemsPrice,
        string source,
        string claimMode)
    {
        if (_claimPending || _window == null)
            return;

        multiplier = Math.Max(1, multiplier);
        gemsPrice = Math.Max(0, gemsPrice);
        RefreshIncomeFromClock();
        OfflineRewardSnapshot preview = BuildSnapshot(_evaluatedAwaySeconds);
        if (preview.BaseIncome <= 0d)
        {
            TrackClaim(
                false,
                multiplier,
                0,
                source,
                claimMode,
                preview,
                0d,
                "nothing_to_collect");
            CloseWindow();
            return;
        }

        if (gemsPrice > 0 &&
            (G.Currency == null ||
             !G.Currency.RemoveCurrency(
                 CurrencyType.Gems,
                 gemsPrice,
                 "offline_reward_x10")))
        {
            TrackClaim(
                false,
                multiplier,
                0,
                source,
                claimMode,
                preview,
                0d,
                "insufficient_gems");
            OpenCurrencyShopAndReturn();
            return;
        }

        _claimPending = true;
        double collectedBase = CollectAllLocalIncome(out int sourceCount);
        if (multiplier > 1 && collectedBase > 0d)
            AddBonusIncome(collectedBase * (multiplier - 1));

        double granted = ApplyIncomeModifiers(collectedBase) * multiplier;
        var resultSnapshot = new OfflineRewardSnapshot(
            _evaluatedAwaySeconds,
            collectedBase,
            ApplyIncomeModifiers(collectedBase),
            sourceCount);

        G.Sound?.Play(
            multiplier > 1
                ? GameAudioId.SFX_REWARD_CLAIM_MAJOR
                : GameAudioId.SFX_OFFLINE_INCOME);
        TrackClaim(
            true,
            multiplier,
            gemsPrice,
            source,
            claimMode,
            resultSnapshot,
            granted,
            string.Empty);
        CloseWindow();
        _claimPending = false;
    }

    private double CollectAllLocalIncome(out int sourceCount)
    {
        double total = 0d;
        sourceCount = 0;
        using (GameAnalytics.BeginIncomeBatch("offline_reward"))
        {
            Brainrot[] animals = Object.FindObjectsByType<Brainrot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < animals.Length; i++)
            {
                if (animals[i] == null || !animals[i].HasCollectibleIncome)
                    continue;

                double collected = animals[i].CollectIncome(false);
                if (collected <= 0d)
                    continue;
                total += collected;
                sourceCount++;
            }

            BigPetPoint[] bigPets = Object.FindObjectsByType<BigPetPoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < bigPets.Length; i++)
            {
                if (bigPets[i] == null || !bigPets[i].HasCollectibleIncome)
                    continue;

                double collected = bigPets[i].CollectIncome(false);
                if (collected <= 0d)
                    continue;
                total += collected;
                sourceCount++;
            }
        }

        return Math.Max(0d, total);
    }

    private static void RefreshIncomeFromClock()
    {
        Brainrot[] animals = Object.FindObjectsByType<Brainrot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < animals.Length; i++)
            animals[i]?.RefreshAccumulatedIncomeFromClock();

        BigPetPoint[] bigPets = Object.FindObjectsByType<BigPetPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < bigPets.Length; i++)
            bigPets[i]?.RefreshAccumulatedIncomeFromClock();
    }

    private static OfflineRewardSnapshot BuildSnapshot(long elapsedSeconds)
    {
        double baseIncome = 0d;
        int sourceCount = 0;

        Brainrot[] animals = Object.FindObjectsByType<Brainrot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < animals.Length; i++)
        {
            Brainrot animal = animals[i];
            if (animal == null || !animal.HasCollectibleIncome)
                continue;
            baseIncome += Math.Max(0d, animal.CurrentIncome);
            sourceCount++;
        }

        BigPetPoint[] bigPets = Object.FindObjectsByType<BigPetPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < bigPets.Length; i++)
        {
            BigPetPoint bigPet = bigPets[i];
            if (bigPet == null || !bigPet.HasCollectibleIncome)
                continue;
            baseIncome += Math.Max(0d, bigPet.AccumulatedIncome);
            sourceCount++;
        }

        return new OfflineRewardSnapshot(
            elapsedSeconds,
            baseIncome,
            ApplyIncomeModifiers(baseIncome),
            sourceCount);
    }

    private static double ApplyIncomeModifiers(double baseIncome)
    {
        if (baseIncome <= 0d)
            return 0d;
        return G.Income != null ? G.Income.Apply(baseIncome) : baseIncome;
    }

    private static void AddBonusIncome(double bonusBaseIncome)
    {
        if (bonusBaseIncome <= 0d)
            return;

        if (G.Income != null)
        {
            G.Income.TryAddCoins(bonusBaseIncome, false);
            return;
        }

        G.Currency?.AddCurrency(CurrencyType.Coins, bonusBaseIncome, false);
    }

    private void OpenCurrencyShopAndReturn()
    {
        if (_window != null)
            _window.SetVisible(false);

        if (G.SpecialShop == null)
        {
            _window?.SetVisible(true);
            return;
        }

        UnsubscribeFromShop();
        G.SpecialShop.Closed += ReopenAfterShop;
        if (!G.SpecialShop.Opened || G.SpecialShop.CurrentCategory != ShopCategory.Currency)
            G.SpecialShop.OpenCurrency();
    }

    private void ReopenAfterShop()
    {
        UnsubscribeFromShop();
        if (_window == null)
            return;

        RefreshIncomeFromClock();
        _window.Refresh(BuildSnapshot(_evaluatedAwaySeconds));
        _window.SetVisible(true);
    }

    private void UnsubscribeFromShop()
    {
        if (G.SpecialShop != null)
            G.SpecialShop.Closed -= ReopenAfterShop;
    }

    private void TrackClaim(
        bool success,
        int multiplier,
        int gemsSpent,
        string source,
        string claimMode,
        OfflineRewardSnapshot snapshot,
        double granted,
        string failureReason)
    {
        GameAnalytics.TrackCritical(AnalyticsEventNames.OfflineRewardClaimResult, GameAnalytics.Params(
            "claim_mode", claimMode,
            "away_duration_sec", snapshot.ElapsedSeconds,
            "credited_duration_sec", snapshot.CappedElapsedSeconds,
            "max_accrual_sec", OfflineRewardRules.MaxAccrualSeconds,
            "base_income", snapshot.BaseIncome,
            "granted_amount", granted,
            "income_source_count", snapshot.SourceCount,
            "multiplier", Math.Max(1, multiplier),
            "gems_spent", success ? Math.Max(0, gemsSpent) : 0,
            "currency_type", "coins",
            "source", string.IsNullOrWhiteSpace(source) ? "offline_reward_window" : source,
            "result", success ? "success" : "failed",
            "failure_reason", failureReason ?? string.Empty),
            "offline_claim:" + claimMode + ":" + (source ?? string.Empty));
    }

    private void CloseWindow()
    {
        UnsubscribeFromShop();
        if (_window != null)
        {
            Destroy(_window.transform.parent.gameObject);
            _window = null;
        }

        if (G.Control != null)
            G.Control.CursorActive = false;
    }

    public long GetMonthlyBoostRemainingSeconds()
    {
        long now = TrustedUtcNowUnix();
        long until = LoadMonthlyBoostUntil();
        if (until <= now)
        {
            if (until > 0L)
            {
                PlayerPrefs.DeleteKey(MonthlyBoostUntilKey);
                PlayerPrefs.Save();
            }

            return 0L;
        }

        return until - now;
    }

    private static long LoadMonthlyBoostUntil()
    {
        string raw = PlayerPrefs.GetString(MonthlyBoostUntilKey, "0");
        return long.TryParse(
            raw,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out long until)
            ? Math.Max(0L, until)
            : 0L;
    }

    private static void PersistLastSeen(long unix)
    {
        if (unix <= 0L || G.Save == null || !G.Save.IsReady)
            return;
        G.Save.SaveOfflineRewardLastSeenUnix(unix);
    }

    private static long TrustedUtcNowUnix()
    {
        try
        {
            DateTime trusted = MirraSDK.Time.CurrentDate.ToUniversalTime();
            if (trusted.Year >= 2020)
                return new DateTimeOffset(trusted).ToUnixTimeSeconds();
        }
        catch
        {
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

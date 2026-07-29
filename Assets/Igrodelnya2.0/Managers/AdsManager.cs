using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdsManager : MonoBehaviour
{
    private const string NoAdsForeverKey = "Ads.NoInterstitialForever";
    private const string NoAdsUntilUnixKey = "Ads.NoInterstitialUntilUnix";

    [SerializeField] private List<AdsProvider> adsProviders = new List<AdsProvider>();

    [Header("Overlay UI")]
    [SerializeField] private AdsOverlayView adOverlayPrefab;

    [Header("Rewarded Ads")]
    [SerializeField] private bool waitForRewardedProvider = true;
    [SerializeField] private float rewardedReadyTimeoutSeconds = 3f;
    [SerializeField] private float rewardedReadyPollSeconds = 0.15f;

    [Header("Timed Interstitial")]
    [SerializeField] private bool timedInterstitialEnabled = true;
    [SerializeField] private float interstitialIntervalSeconds = 60f;
    [SerializeField] private int interstitialCountdownSeconds = 3;
    [SerializeField] private double interstitialIncomeRewardMultiplier = 2d;
    [SerializeField] private string timedInterstitialCountdownLocalizationKey = "UI/Ads/TimedInterstitialCountdown";
    [SerializeField] private string timedInterstitialCountdownText = "Ad in {0}";
    [SerializeField] private string timedInterstitialRewardLocalizationKey = "UI/Ads/TimedInterstitialReward";
    [SerializeField] private string timedInterstitialRewardText = "Reward: +{0}";

    public Action AdClosed;

    private bool _rewardedInProgress;
    private bool _interstitialInProgress;
    private Coroutine _timedInterstitialRoutine;
    private Coroutine _adButtonIconRoutine;
    private Coroutine _rewardPopupRoutine;
    private float _lastSuccessfulAdRealtime;
    private bool _timedInterstitialFlowInProgress;
    private bool _tutorialInterstitialSuppressed;
    private float _tutorialInterstitialGraceUntilRealtime;

    private Canvas _adOverlayCanvas;
    private RectTransform _countdownPanel;
    private TextMeshProUGUI _countdownText;
    private TextMeshProUGUI _countdownRewardText;
    private RectTransform _rewardTextRect;
    private TextMeshProUGUI _rewardText;

    public bool AreInterstitialAdsDisabled
    {
        get
        {
            if (PlayerPrefs.GetInt(NoAdsForeverKey, 0) == 1)
                return true;

            long untilUnix = GetNoAdsUntilUnix();
            if (untilUnix <= 0)
                return false;

            if (untilUnix > GetCurrentUtcUnixSeconds())
                return true;

            PlayerPrefs.DeleteKey(NoAdsUntilUnixKey);
            PlayerPrefs.Save();
            return false;
        }
    }

    private void Awake()
    {
        if (G.Ad == null)
        {
            G.Ad = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeProviders();
    }

    private void Start()
    {
        SubscribeProviders(true);
        ResetTimedInterstitialTimer();
        StartTimedInterstitialRoutine();
        StartAdButtonIconRoutine();
    }

    private void OnDisable()
    {
        SubscribeProviders(false);
    }

    private void OnAdClosed()
    {
        ResetTimedInterstitialTimer();
        AdClosed?.Invoke();
    }

    private void InitializeProviders()
    {
        for (int i = 0; i < adsProviders.Count; i++)
        {
            if (adsProviders[i] == null)
                continue;

            adsProviders[i].Initialize();
        }
    }

    private void SubscribeProviders(bool subscribe)
    {
        for (int i = 0; i < adsProviders.Count; i++)
        {
            AdsProvider provider = adsProviders[i];
            if (provider == null)
                continue;

            if (subscribe)
                provider.AdClosed += OnAdClosed;
            else
                provider.AdClosed -= OnAdClosed;
        }
    }

    public bool IsRewardedAdReady()
    {
        return FindReadyRewardedProvider() != null;
    }

    public void ShowRewardedAd(string rewardId, Action<bool> onComplete)
    {
        string requestId = Guid.NewGuid().ToString("N");
        float requestedAt = Time.realtimeSinceStartup;
        TrackAdRequested("rewarded", rewardId, requestId);

        if (_rewardedInProgress)
        {
            Debug.LogWarning("Rewarded ad is already in progress.");
            TrackAdResult("rewarded", rewardId, requestId, null, requestedAt, false, "already_in_progress");
            onComplete?.Invoke(false);
            return;
        }

        AdsProvider provider = FindReadyRewardedProvider();
        if (provider != null)
        {
            ShowRewardedAd(provider, rewardId, requestId, requestedAt, onComplete);
            return;
        }

        if (!waitForRewardedProvider)
        {
            Debug.LogWarning("No rewarded ads available.");
            TrackAdResult("rewarded", rewardId, requestId, null, requestedAt, false, "provider_unavailable");
            onComplete?.Invoke(false);
            return;
        }

        _rewardedInProgress = true;
        StartCoroutine(WaitAndShowRewardedAd(rewardId, requestId, requestedAt, onComplete));
    }

    public void ShowInterstitialAd()
    {
        ShowInterstitialAd(null);
    }

    public void ShowInterstitialAd(Action<bool> onComplete)
    {
        TryShowInterstitialAd(onComplete);
    }

    private bool TryShowInterstitialAd(Action<bool> onComplete)
    {
        string requestId = Guid.NewGuid().ToString("N");
        float requestedAt = Time.realtimeSinceStartup;
        TrackAdRequested("interstitial", "interstitial", requestId);

        if (IsInterstitialTemporarilySuppressed)
        {
            TrackAdResult("interstitial", "interstitial", requestId, null, requestedAt, false, "tutorial_suppressed");
            onComplete?.Invoke(false);
            return false;
        }

        if (AreInterstitialAdsDisabled)
        {
            TrackAdResult("interstitial", "interstitial", requestId, null, requestedAt, false, "no_ads_unlocked");
            onComplete?.Invoke(false);
            return false;
        }

        if (_interstitialInProgress)
        {
            Debug.LogWarning("Interstitial ad is already in progress.");
            TrackAdResult("interstitial", "interstitial", requestId, null, requestedAt, false, "already_in_progress");
            onComplete?.Invoke(false);
            return false;
        }

        AdsProvider provider = FindReadyInterstitialProvider();
        if (provider == null)
        {
            Debug.LogWarning("No interstitial ads available.");
            TrackAdResult("interstitial", "interstitial", requestId, null, requestedAt, false, "provider_unavailable");
            onComplete?.Invoke(false);
            return false;
        }

        _interstitialInProgress = true;
        ResetTimedInterstitialTimer();
        provider.ShowInterstitialAd(success =>
        {
            _interstitialInProgress = false;
            RegisterAdWatched(success);
            TrackAdResult("interstitial", "interstitial", requestId, provider, requestedAt, success,
                success ? string.Empty : "provider_failed");
            onComplete?.Invoke(success);
        });

        return true;
    }

    public void DisableInterstitialAdsForDays(int days)
    {
        bool wasDisabled = AreInterstitialAdsDisabled;
        int safeDays = Mathf.Max(1, days);
        long nowUnix = GetCurrentUtcUnixSeconds();
        long currentUntilUnix = Math.Max(GetNoAdsUntilUnix(), nowUnix);
        long until = DateTimeOffset.FromUnixTimeSeconds(currentUntilUnix).AddDays(safeDays).ToUnixTimeSeconds();
        PlayerPrefs.SetString(NoAdsUntilUnixKey, until.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        if (!wasDisabled)
            TrackNoAdsUnlocked("days", safeDays);
    }

    public void DisableInterstitialAdsForever()
    {
        bool wasDisabled = AreInterstitialAdsDisabled;
        PlayerPrefs.SetInt(NoAdsForeverKey, 1);
        PlayerPrefs.Save();
        if (!wasDisabled)
            TrackNoAdsUnlocked("forever", 0);
    }

    public bool IsInterstitialTemporarilySuppressed =>
        _tutorialInterstitialSuppressed || Time.realtimeSinceStartup < _tutorialInterstitialGraceUntilRealtime;

    public void SetTutorialInterstitialSuppressed(bool suppressed, float graceSeconds = 0f)
    {
        _tutorialInterstitialSuppressed = suppressed;
        if (suppressed)
        {
            _tutorialInterstitialGraceUntilRealtime = 0f;
            HideCountdown();
        }
        else
        {
            _tutorialInterstitialGraceUntilRealtime = Time.realtimeSinceStartup + Mathf.Max(0f, graceSeconds);
        }

        ResetTimedInterstitialTimer();
    }

    public void AddProvider(AdsProvider provider)
    {
        if (provider == null || adsProviders.Contains(provider))
            return;

        adsProviders.Add(provider);
        provider.Initialize();
        provider.AdClosed += OnAdClosed;
    }

    private AdsProvider FindReadyRewardedProvider()
    {
        if (adsProviders == null || adsProviders.Count == 0)
            return null;

        for (int i = 0; i < adsProviders.Count; i++)
        {
            AdsProvider provider = adsProviders[i];
            if (provider != null && provider.IsInitialized && provider.IsRewardedAdReady())
                return provider;
        }

        return null;
    }

    private AdsProvider FindReadyInterstitialProvider()
    {
        if (adsProviders == null || adsProviders.Count == 0)
            return null;

        for (int i = 0; i < adsProviders.Count; i++)
        {
            AdsProvider provider = adsProviders[i];
            if (provider != null && provider.IsInitialized && provider.IsInterstitialAdReady())
                return provider;
        }

        return null;
    }

    private void ShowRewardedAd(
        AdsProvider provider,
        string rewardId,
        string requestId,
        float requestedAt,
        Action<bool> onComplete)
    {
        _rewardedInProgress = true;
        ResetTimedInterstitialTimer();
        provider.ShowRewardedAd(rewardId, success =>
        {
            _rewardedInProgress = false;
            RegisterAdWatched(success);
            TrackAdResult("rewarded", rewardId, requestId, provider, requestedAt, success,
                success ? string.Empty : "provider_failed");
            onComplete?.Invoke(success);
        });
    }

    private IEnumerator WaitAndShowRewardedAd(
        string rewardId,
        string requestId,
        float requestedAt,
        Action<bool> onComplete)
    {
        float timeout = Mathf.Max(0f, rewardedReadyTimeoutSeconds);
        WaitForSecondsRealtime poll = new WaitForSecondsRealtime(Mathf.Max(0.02f, rewardedReadyPollSeconds));
        float startedAt = Time.realtimeSinceStartup;
        AdsProvider provider = null;

        while (Time.realtimeSinceStartup - startedAt <= timeout)
        {
            provider = FindReadyRewardedProvider();
            if (provider != null)
                break;

            yield return poll;
        }

        if (provider == null)
        {
            Debug.LogWarning("No rewarded ads available after wait.");
            _rewardedInProgress = false;
            TrackAdResult("rewarded", rewardId, requestId, null, requestedAt, false, "ready_timeout");
            onComplete?.Invoke(false);
            yield break;
        }

        ResetTimedInterstitialTimer();
        provider.ShowRewardedAd(rewardId, success =>
        {
            _rewardedInProgress = false;
            RegisterAdWatched(success);
            TrackAdResult("rewarded", rewardId, requestId, provider, requestedAt, success,
                success ? string.Empty : "provider_failed");
            onComplete?.Invoke(success);
        });
    }

    private void StartTimedInterstitialRoutine()
    {
        if (!timedInterstitialEnabled || _timedInterstitialRoutine != null)
            return;

        _timedInterstitialRoutine = StartCoroutine(TimedInterstitialRoutine());
    }

    private void StartAdButtonIconRoutine()
    {
        if (_adButtonIconRoutine != null)
            return;

        _adButtonIconRoutine = StartCoroutine(AdButtonIconRoutine());
    }

    private IEnumerator AdButtonIconRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1f);

        for (int i = 0; i < 10; i++)
        {
            AdButtonIconDecorator.DecorateScene();
            yield return wait;
        }

        _adButtonIconRoutine = null;
    }

    private IEnumerator TimedInterstitialRoutine()
    {
        yield return null;

        while (timedInterstitialEnabled)
        {
            yield return WaitForTimedInterstitialInterval();

            while (timedInterstitialEnabled && !CanShowTimedInterstitialNow())
                yield return null;

            if (!timedInterstitialEnabled)
                break;

            yield return ShowTimedInterstitialFlow();
        }

        _timedInterstitialRoutine = null;
    }

    private IEnumerator WaitForTimedInterstitialInterval()
    {
        while (timedInterstitialEnabled)
        {
            if (CanCountTimedInterstitialTime() && HasTimedInterstitialIntervalElapsed())
                yield break;

            yield return null;
        }
    }

    private IEnumerator ShowTimedInterstitialFlow()
    {
        if (_timedInterstitialFlowInProgress)
            yield break;

        _timedInterstitialFlowInProgress = true;
        ResetTimedInterstitialTimer();

        double baseReward = CalculateTimedInterstitialBaseReward();
        int countdown = Mathf.Max(1, interstitialCountdownSeconds);
        for (int i = countdown; i > 0; i--)
        {
            if (!CanContinueTimedInterstitialFlow())
            {
                HideCountdown();
                _timedInterstitialFlowInProgress = false;
                yield break;
            }

            ShowCountdown(i, baseReward);
            yield return new WaitForSecondsRealtime(1f);
        }

        HideCountdown();

        bool done = false;
        bool adSuccess = false;
        bool adStarted = TryShowInterstitialAd(success =>
        {
            adSuccess = success;
            done = true;
        });

        if (!adStarted)
        {
            _timedInterstitialFlowInProgress = false;
            yield break;
        }

        while (!done)
            yield return null;

        if (adSuccess)
            GiveTimedInterstitialReward(baseReward);

        _timedInterstitialFlowInProgress = false;
    }

    private bool CanCountTimedInterstitialTime()
    {
        if (AreInterstitialAdsDisabled || IsInterstitialTemporarilySuppressed || _interstitialInProgress || _rewardedInProgress)
            return false;

        if (G.Control != null && G.Control.CursorActive)
            return false;

        return true;
    }

    private bool CanShowTimedInterstitialNow()
    {
        if (!CanCountTimedInterstitialTime())
            return false;

        if (!HasTimedInterstitialIntervalElapsed())
            return false;

        return FindReadyInterstitialProvider() != null;
    }

    private bool CanContinueTimedInterstitialFlow()
    {
        if (!CanCountTimedInterstitialTime())
            return false;

        return FindReadyInterstitialProvider() != null;
    }

    private void GiveTimedInterstitialReward(double baseReward)
    {
        if (baseReward <= 0d)
            return;

        double finalReward = CalculateFinalRewardAmount(baseReward);
        if (G.Currency == null)
        {
            Debug.LogError("[AdsManager] Cannot give timed interstitial reward: CurrencyManager is not initialized.");
            return;
        }

        G.Currency.AddCurrency(CurrencyType.Coins, finalReward);
        GameAnalytics.Track(AnalyticsEventNames.TimedInterstitialRewardGranted, GameAnalytics.Params(
            "placement", "timed_interstitial",
            "currency_type", "coins",
            "reward_amount", finalReward,
            "base_reward_amount", baseReward,
            "multiplier", interstitialIncomeRewardMultiplier,
            "source", "timed_interstitial",
            "result", "success"));

        ShowRewardPopup(finalReward);
    }

    private static void TrackAdRequested(string adType, string placement, string requestId)
    {
        GameAnalytics.Track(AnalyticsEventNames.AdRequested, GameAnalytics.Params(
            "ad_type", adType,
            "placement", placement ?? string.Empty,
            "request_id", requestId,
            "source", placement ?? string.Empty,
            "result", "requested"));
    }

    private static void TrackAdResult(
        string adType,
        string placement,
        string requestId,
        AdsProvider provider,
        float requestedAt,
        bool success,
        string failureReason)
    {
        GameAnalytics.Track(AnalyticsEventNames.AdResult, GameAnalytics.Params(
            "ad_type", adType,
            "placement", placement ?? string.Empty,
            "request_id", requestId,
            "provider", provider != null ? provider.GetType().Name : string.Empty,
            "latency_ms", Math.Round(Math.Max(0f, Time.realtimeSinceStartup - requestedAt) * 1000d),
            "reward_granted", success && string.Equals(adType, "rewarded", StringComparison.Ordinal),
            "source", placement ?? string.Empty,
            "result", success ? "success" : "failed",
            "failure_reason", failureReason ?? string.Empty),
            AnalyticsPriority.Normal,
            requestId);
    }

    private static void TrackNoAdsUnlocked(string unlockType, int days)
    {
        AnalyticsItemGrantContext context = AnalyticsContext.ItemGrant;
        GameAnalytics.TrackCritical(AnalyticsEventNames.NoAdsUnlocked, GameAnalytics.Params(
            "unlock_type", unlockType,
            "duration_days", days,
            "source", context != null ? context.Source : "shop",
            "source_id", context != null ? context.SourceId : string.Empty,
            "currency_type", context != null ? context.CurrencyType : string.Empty,
            "price", context != null ? context.Price : 0d,
            "result", "success"),
            "no_ads:" + unlockType);
    }

    private double CalculateTimedInterstitialBaseReward()
    {
        double incomePerSecond = CalculateCurrentBaseIncomePerSecond();
        return Math.Round(Math.Max(0d, incomePerSecond * Math.Max(0d, interstitialIncomeRewardMultiplier)));
    }

    private double CalculateFinalRewardAmount(double baseReward)
    {
        if (baseReward <= 0d)
            return 0d;

        return G.Income != null ? G.Income.Apply(baseReward) : baseReward;
    }

    private bool HasTimedInterstitialIntervalElapsed()
    {
        return Time.realtimeSinceStartup - _lastSuccessfulAdRealtime >= Mathf.Max(5f, interstitialIntervalSeconds);
    }

    private void ResetTimedInterstitialTimer()
    {
        _lastSuccessfulAdRealtime = Time.realtimeSinceStartup;
    }

    private void RegisterAdWatched(bool success)
    {
        ResetTimedInterstitialTimer();
    }

    private double CalculateCurrentBaseIncomePerSecond()
    {
        double total = 0d;
        FieldCell[] cells = FindObjectsByType<FieldCell>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cells.Length; i++)
        {
            FieldCell cell = cells[i];
            if (cell == null || cell.CurrentBrainrot == null)
                continue;

            Field field = cell.GetComponentInParent<Field>();
            if (field == null || field.IsRemoteMode)
                continue;

            total += Math.Max(0d, cell.CurrentBrainrot.DinamicData.ResultIncome);
        }

        BigPetPoint[] bigPets = FindObjectsByType<BigPetPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < bigPets.Length; i++)
        {
            BigPetPoint bigPet = bigPets[i];
            if (bigPet == null || bigPet.IsRemoteMode)
                continue;

            total += Math.Max(0d, bigPet.CurrentIncomePerSecond);
        }

        return total;
    }

    private void EnsureAdOverlay()
    {
        if (_adOverlayCanvas != null)
            return;

        if (TryCreateAdOverlayFromPrefab())
            return;

        CreateRuntimeAdOverlay();
    }

    private bool TryCreateAdOverlayFromPrefab()
    {
        if (adOverlayPrefab == null)
            return false;

        AdsOverlayView view = Instantiate(adOverlayPrefab);
        view.name = "AdsOverlayCanvas";
        DontDestroyOnLoad(view.gameObject);

        _adOverlayCanvas = view.Canvas;
        _countdownPanel = view.CountdownPanel;
        _countdownText = view.CountdownText;
        _countdownRewardText = view.CountdownRewardText;
        _rewardText = view.RewardText;
        _rewardTextRect = view.RewardTextRect;

        if (_adOverlayCanvas == null ||
            _countdownPanel == null ||
            _countdownText == null ||
            _rewardText == null ||
            _rewardTextRect == null)
        {
            Debug.LogWarning("[AdsManager] Ads overlay prefab is missing required references. Falling back to runtime overlay.");
            Destroy(view.gameObject);
            _adOverlayCanvas = null;
            _countdownPanel = null;
            _countdownText = null;
            _countdownRewardText = null;
            _rewardText = null;
            _rewardTextRect = null;
            return false;
        }

        _countdownPanel.gameObject.SetActive(false);
        _rewardText.gameObject.SetActive(false);
        view.gameObject.SetActive(false);
        return true;
    }

    private void CreateRuntimeAdOverlay()
    {
        GameObject canvasObject = new GameObject("AdsOverlayCanvas");
        DontDestroyOnLoad(canvasObject);

        _adOverlayCanvas = canvasObject.AddComponent<Canvas>();
        _adOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _adOverlayCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("CountdownPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        _countdownPanel = panelObject.AddComponent<RectTransform>();
        _countdownPanel.anchorMin = new Vector2(0.5f, 0.5f);
        _countdownPanel.anchorMax = new Vector2(0.5f, 0.5f);
        _countdownPanel.pivot = new Vector2(0.5f, 0.5f);
        _countdownPanel.anchoredPosition = Vector2.zero;
        _countdownPanel.sizeDelta = new Vector2(640f, 240f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;

        _countdownText = CreateOverlayText("CountdownText", _countdownPanel, 54f, Color.white);
        _countdownText.rectTransform.anchorMin = new Vector2(0f, 0.38f);
        _countdownText.rectTransform.anchorMax = Vector2.one;
        _countdownText.rectTransform.offsetMin = new Vector2(28f, 0f);
        _countdownText.rectTransform.offsetMax = new Vector2(-28f, -10f);

        _countdownRewardText = CreateOverlayText("CountdownRewardText", _countdownPanel, 34f, new Color(1f, 0.92f, 0.24f, 1f));
        _countdownRewardText.rectTransform.anchorMin = new Vector2(0f, 0.06f);
        _countdownRewardText.rectTransform.anchorMax = new Vector2(1f, 0.38f);
        _countdownRewardText.rectTransform.offsetMin = new Vector2(28f, 6f);
        _countdownRewardText.rectTransform.offsetMax = new Vector2(-28f, 0f);

        _rewardText = CreateOverlayText("InterstitialRewardText", canvasObject.transform, 64f, new Color(1f, 0.92f, 0.24f, 1f));
        _rewardTextRect = _rewardText.rectTransform;
        _rewardTextRect.anchorMin = new Vector2(0.5f, 0.55f);
        _rewardTextRect.anchorMax = new Vector2(0.5f, 0.55f);
        _rewardTextRect.pivot = new Vector2(0.5f, 0.5f);
        _rewardTextRect.sizeDelta = new Vector2(760f, 110f);
        _rewardText.gameObject.SetActive(false);

        _countdownPanel.gameObject.SetActive(false);
        canvasObject.SetActive(false);
    }

    private TextMeshProUGUI CreateOverlayText(string objectName, Transform parent, float fontSize, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600f, 120f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = 24f;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void ShowCountdown(int seconds, double baseReward)
    {
        EnsureAdOverlay();
        _adOverlayCanvas.gameObject.SetActive(true);
        _countdownPanel.gameObject.SetActive(true);
        var countdownFormat = LocalizationUtils.T(timedInterstitialCountdownLocalizationKey, timedInterstitialCountdownText);
        _countdownText.text = string.Format(CultureInfo.InvariantCulture, countdownFormat, seconds);
        if (_countdownRewardText != null)
        {
            double finalReward = CalculateFinalRewardAmount(baseReward);
            var rewardFormat = LocalizationUtils.T(timedInterstitialRewardLocalizationKey, timedInterstitialRewardText);
            _countdownRewardText.text = string.Format(CultureInfo.InvariantCulture, rewardFormat, FormatCoins(finalReward));
            _countdownRewardText.gameObject.SetActive(true);
        }
    }

    private void HideCountdown()
    {
        if (_countdownPanel != null)
            _countdownPanel.gameObject.SetActive(false);

        TryHideAdOverlay();
    }

    private void ShowRewardPopup(double amount)
    {
        EnsureAdOverlay();
        if (_rewardPopupRoutine != null)
            StopCoroutine(_rewardPopupRoutine);

        _rewardPopupRoutine = StartCoroutine(RewardPopupRoutine(amount));
    }

    private IEnumerator RewardPopupRoutine(double amount)
    {
        _adOverlayCanvas.gameObject.SetActive(true);
        _rewardText.gameObject.SetActive(true);
        _rewardText.text = "+" + FormatCoins(amount);

        Vector2 startPosition = new Vector2(0f, 0f);
        Vector2 endPosition = new Vector2(0f, 90f);
        Color baseColor = new Color(1f, 0.92f, 0.24f, 1f);
        float duration = 1.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = t < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f);
            float scale = Mathf.Lerp(0.85f, 1.15f, Mathf.Sin(t * Mathf.PI));

            _rewardTextRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
            _rewardTextRect.localScale = Vector3.one * scale;
            _rewardText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }

        _rewardText.gameObject.SetActive(false);
        _rewardTextRect.anchoredPosition = startPosition;
        _rewardTextRect.localScale = Vector3.one;
        _rewardText.color = baseColor;
        _rewardPopupRoutine = null;
        TryHideAdOverlay();
    }

    private void TryHideAdOverlay()
    {
        if (_adOverlayCanvas == null)
            return;

        bool countdownActive = _countdownPanel != null && _countdownPanel.gameObject.activeSelf;
        bool rewardActive = _rewardText != null && _rewardText.gameObject.activeSelf;
        if (!countdownActive && !rewardActive)
            _adOverlayCanvas.gameObject.SetActive(false);
    }

    private string FormatCoins(double amount)
    {
        if (G.Currency != null)
            return G.Currency.ToString(amount);

        return Math.Round(amount).ToString("0", CultureInfo.InvariantCulture);
    }

    private long GetNoAdsUntilUnix()
    {
        string raw = PlayerPrefs.GetString(NoAdsUntilUnixKey, "0");
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0L;
    }

    private static long GetCurrentUtcUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

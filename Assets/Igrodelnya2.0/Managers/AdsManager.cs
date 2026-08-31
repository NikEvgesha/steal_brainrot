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
    private const int InterstitialWarningSeconds = 2;

    [SerializeField] private List<AdsProvider> adsProviders = new List<AdsProvider>();

    [Header("Overlay UI")]
    [SerializeField] private AdsOverlayView adOverlayPrefab;

    [Header("Rewarded Ads")]
    [SerializeField] private bool waitForRewardedProvider = true;
    [SerializeField] private float rewardedReadyTimeoutSeconds = 3f;
    [SerializeField] private float rewardedReadyPollSeconds = 0.15f;

    [Header("Startup Interstitial")]
    [SerializeField] private bool startupInterstitialEnabled = true;
    [SerializeField, Min(0f)] private float startupInterstitialDelaySeconds = 0.5f;
    [SerializeField, Min(1f)] private float startupInterstitialReadyTimeoutSeconds = 20f;
    [SerializeField, Min(0.02f)] private float startupInterstitialReadyPollSeconds = 0.25f;
    [SerializeField, Min(3f)] private float interstitialCallbackTimeoutSeconds = 20f;

    [Header("Timed Reward")]
    [SerializeField] private bool timedInterstitialEnabled = true;
    [SerializeField] private float interstitialIntervalSeconds = 60f;
    [SerializeField] private double interstitialIncomeRewardMultiplier = 2d;
    [SerializeField] private string timedRewardedAdId = "TimedIncomeRewardX2";
    [SerializeField] private string timedInterstitialCountdownLocalizationKey = "UI/Ads/TimedInterstitialCountdown";
    [SerializeField] private string timedInterstitialCountdownText = "Ad in {0}";
    [SerializeField] private string timedRewardX2ButtonText = "X2";
    [SerializeField] private string timedInterstitialRewardLocalizationKey = "UI/Ads/TimedInterstitialReward";
    [SerializeField] private string timedInterstitialRewardText = "Reward: +{0}";

    public Action AdClosed;

    private bool _rewardedInProgress;
    private bool _interstitialInProgress;
    private int _interstitialRequestVersion;
    private bool _runtimeStarted;
    private bool _startupInterstitialFinished;
    private Coroutine _startupInterstitialRoutine;
    private Coroutine _interstitialCallbackWatchdogRoutine;
    private Coroutine _timedInterstitialRoutine;
    private Coroutine _adButtonIconRoutine;
    private Coroutine _rewardPopupRoutine;
    private Coroutine _rewardedCallbackWatchdogRoutine;
#if UNITY_EDITOR
    private Coroutine _editorCountdownPreviewRoutine;
#endif
    private float _lastSuccessfulAdRealtime;
    private bool _timedInterstitialFlowInProgress;
    private bool _tutorialInterstitialSuppressed;
    private float _tutorialInterstitialGraceUntilRealtime;
    private int _adPauseDepth;
    private bool _wasGloballyPausedBeforeAd;
    private PauseManager _adPauseManager;
    private float _timeScaleBeforeAd;
    private bool _audioPauseBeforeAd;

    private Canvas _adOverlayCanvas;
    private RectTransform _countdownPanel;
    private TextMeshProUGUI _countdownText;
    private TextMeshProUGUI _countdownRewardText;
    private RectTransform _rewardTextRect;
    private TextMeshProUGUI _rewardText;
    private Button _timedRewardX2Button;
    private TextMeshProUGUI _timedRewardX2ButtonText;
    private Image _adInputBlocker;
    private bool _usesPrefabOverlayLayout;
    private bool _timedRewardX2ButtonInitialized;

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

    public bool ShouldDeferStartupModal =>
        (startupInterstitialEnabled && !_startupInterstitialFinished) ||
        _interstitialInProgress ||
        _rewardedInProgress ||
        _adPauseDepth > 0;

    public bool StartupInterstitialFinished =>
        !startupInterstitialEnabled || _startupInterstitialFinished;

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
        EnsureRuntimeStarted();
    }

    private void Start()
    {
        EnsureRuntimeStarted();
    }

    private void EnsureRuntimeStarted()
    {
        if (_runtimeStarted)
            return;

        _runtimeStarted = true;
        SubscribeProviders(true);
        ResetTimedInterstitialTimer();
        EnsureAdRoutinesRunning();
    }

    private void EnsureAdRoutinesRunning()
    {
        StartStartupInterstitialRoutine();
        StartTimedInterstitialRoutine();
        StartAdButtonIconRoutine();
    }

    private void Update()
    {
        if (!_runtimeStarted)
            return;

        if (_interstitialInProgress && _interstitialCallbackWatchdogRoutine == null)
        {
            _interstitialInProgress = false;
            _interstitialRequestVersion++;
            ReleaseAdPause();
        }

        StartStartupInterstitialRoutine();
        StartTimedInterstitialRoutine();
    }

    private void OnDisable()
    {
        SubscribeProviders(false);
    }

    private void OnDestroy()
    {
        while (_adPauseDepth > 0)
            ReleaseAdPause();
    }

    private void OnAdClosed()
    {
        ResetTimedInterstitialTimer();
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

    private bool TryShowInterstitialAd(
        Action<bool> onComplete,
        bool ignoreTemporarySuppression = false,
        string placement = "interstitial")
    {
        string requestId = Guid.NewGuid().ToString("N");
        float requestedAt = Time.realtimeSinceStartup;
        TrackAdRequested("interstitial", placement, requestId);

        if (!ignoreTemporarySuppression && IsInterstitialTemporarilySuppressed)
        {
            TrackAdResult("interstitial", placement, requestId, null, requestedAt, false, "tutorial_suppressed");
            onComplete?.Invoke(false);
            return false;
        }

        if (AreInterstitialAdsDisabled)
        {
            TrackAdResult("interstitial", placement, requestId, null, requestedAt, false, "no_ads_unlocked");
            onComplete?.Invoke(false);
            return false;
        }

        if (_interstitialInProgress)
        {
            Debug.LogWarning("Interstitial ad is already in progress.");
            TrackAdResult("interstitial", placement, requestId, null, requestedAt, false, "already_in_progress");
            onComplete?.Invoke(false);
            return false;
        }

        AdsProvider provider = FindReadyInterstitialProvider();
        if (provider == null)
        {
            Debug.LogWarning("No interstitial ads available.");
            TrackAdResult("interstitial", placement, requestId, null, requestedAt, false, "provider_unavailable");
            onComplete?.Invoke(false);
            return false;
        }

        _interstitialInProgress = true;
        ResetTimedInterstitialTimer();
        int requestVersion = ++_interstitialRequestVersion;
        bool resolved = false;
        AcquireAdPause();

        void ResolveRequest(bool success, string failureReason)
        {
            if (resolved || requestVersion != _interstitialRequestVersion)
                return;

            resolved = true;
            if (_interstitialCallbackWatchdogRoutine != null)
            {
                StopCoroutine(_interstitialCallbackWatchdogRoutine);
                _interstitialCallbackWatchdogRoutine = null;
            }

            _interstitialInProgress = false;
            RegisterAdWatched(success);
            TrackAdResult("interstitial", placement, requestId, provider, requestedAt, success,
                success ? string.Empty : failureReason);
            ReleaseAdPause();
            onComplete?.Invoke(success);
        }

        _interstitialCallbackWatchdogRoutine = StartCoroutine(
            InterstitialCallbackWatchdog(
                requestVersion,
                () => ResolveRequest(false, "callback_timeout")));

        try
        {
            provider.ShowInterstitialAd(success =>
                ResolveRequest(success, success ? string.Empty : "provider_failed"));
        }
        catch (Exception exception)
        {
            Debug.LogError($"[AdsManager] Interstitial provider threw an exception: {exception.Message}");
            ResolveRequest(false, "provider_exception");
        }

        return true;
    }

    private IEnumerator InterstitialCallbackWatchdog(int requestVersion, Action onTimeout)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(3f, interstitialCallbackTimeoutSeconds));

        if (requestVersion != _interstitialRequestVersion || !_interstitialInProgress)
            yield break;

        _interstitialCallbackWatchdogRoutine = null;
        Debug.LogWarning("[AdsManager] Interstitial callback timed out; releasing the ad lock.");
        onTimeout?.Invoke();
    }

    private void StartStartupInterstitialRoutine()
    {
        if (!startupInterstitialEnabled || _startupInterstitialFinished || _startupInterstitialRoutine != null)
            return;

        _startupInterstitialRoutine = StartCoroutine(StartupInterstitialRoutine());
    }

    private IEnumerator StartupInterstitialRoutine()
    {
        if (startupInterstitialDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(startupInterstitialDelaySeconds);

        float startedAt = Time.realtimeSinceStartup;
        float timeout = Mathf.Max(1f, startupInterstitialReadyTimeoutSeconds);
        WaitForSecondsRealtime poll = new WaitForSecondsRealtime(
            Mathf.Max(0.02f, startupInterstitialReadyPollSeconds));

        while (Time.realtimeSinceStartup - startedAt <= timeout)
        {
            if (AreInterstitialAdsDisabled)
            {
                Debug.Log("[AdsManager] Startup interstitial skipped: interstitial ads are disabled.");
                _startupInterstitialFinished = true;
                _startupInterstitialRoutine = null;
                yield break;
            }

            if (!_interstitialInProgress && !_rewardedInProgress && FindReadyInterstitialProvider() != null)
            {
                bool requestCompleted = false;
                bool started = TryShowInterstitialAd(
                    _ => requestCompleted = true,
                    ignoreTemporarySuppression: true,
                    placement: "startup");
                Debug.Log(started
                    ? "[AdsManager] Startup interstitial requested."
                    : "[AdsManager] Startup interstitial request was rejected.");

                if (started)
                {
                    while (!requestCompleted)
                        yield return null;
                }

                _startupInterstitialFinished = true;
                _startupInterstitialRoutine = null;
                yield break;
            }

            yield return poll;
        }

        Debug.LogWarning($"[AdsManager] Startup interstitial was not ready within {timeout:0.#} seconds.");
        _startupInterstitialFinished = true;
        _startupInterstitialRoutine = null;
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
        }
        else
        {
            _tutorialInterstitialGraceUntilRealtime = Time.realtimeSinceStartup + Mathf.Max(0f, graceSeconds);
        }
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
        bool resolved = false;
        AcquireAdPause();

        void ResolveRequest(bool success, string failureReason)
        {
            if (resolved)
                return;

            resolved = true;
            if (_rewardedCallbackWatchdogRoutine != null)
            {
                StopCoroutine(_rewardedCallbackWatchdogRoutine);
                _rewardedCallbackWatchdogRoutine = null;
            }

            _rewardedInProgress = false;
            RegisterAdWatched(success);
            TrackAdResult("rewarded", rewardId, requestId, provider, requestedAt, success,
                success ? string.Empty : failureReason);
            ReleaseAdPause();
            onComplete?.Invoke(success);
        }

        _rewardedCallbackWatchdogRoutine = StartCoroutine(
            RewardedCallbackWatchdog(() => ResolveRequest(false, "callback_timeout")));

        try
        {
            provider.ShowRewardedAd(rewardId, success =>
                ResolveRequest(success, success ? string.Empty : "provider_failed"));
        }
        catch (Exception exception)
        {
            Debug.LogError($"[AdsManager] Rewarded provider threw an exception: {exception.Message}");
            ResolveRequest(false, "provider_exception");
        }
    }

    private IEnumerator RewardedCallbackWatchdog(Action onTimeout)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(10f, interstitialCallbackTimeoutSeconds));
        _rewardedCallbackWatchdogRoutine = null;
        Debug.LogWarning("[AdsManager] Rewarded callback timed out; releasing the ad pause.");
        onTimeout?.Invoke();
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

        ShowRewardedAd(provider, rewardId, requestId, requestedAt, onComplete);
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

            yield return ShowTimedRewardFlow();
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

    private IEnumerator ShowTimedRewardFlow()
    {
        if (_timedInterstitialFlowInProgress)
            yield break;

        _timedInterstitialFlowInProgress = true;
        ResetTimedInterstitialTimer();
        AcquireAdPause();
        bool countdownPauseHeld = true;

        void ReleaseCountdownPause()
        {
            if (!countdownPauseHeld)
                return;

            countdownPauseHeld = false;
            ReleaseAdPause();
        }

        double baseReward = CalculateTimedInterstitialBaseReward();
        bool x2Requested = false;
        bool rewardedResolved = false;
        bool rewardedSuccess = false;

        void RequestX2Reward()
        {
            if (x2Requested)
                return;

            x2Requested = true;
            if (_timedRewardX2Button != null)
                _timedRewardX2Button.interactable = false;

            HideCountdown();
            ShowRewardedAd(timedRewardedAdId, success =>
            {
                rewardedSuccess = success;
                rewardedResolved = true;
            });
        }

        int countdown = InterstitialWarningSeconds;
        for (int i = countdown; i > 0; i--)
        {
            if (!CanContinueTimedInterstitialFlow())
            {
                HideCountdown();
                ReleaseCountdownPause();
                _timedInterstitialFlowInProgress = false;
                yield break;
            }

            ShowCountdown(i, baseReward, RequestX2Reward);
            float elapsed = 0f;
            while (!x2Requested && elapsed < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (x2Requested)
                break;
        }

        HideCountdown();

        if (x2Requested)
        {
            while (!rewardedResolved)
                yield return null;

            if (rewardedSuccess)
            {
                GiveTimedReward(baseReward, "timed_reward_x2", 2d);
                ReleaseCountdownPause();
                _timedInterstitialFlowInProgress = false;
                yield break;
            }

            // If the rewarded ad was cancelled or failed, keep the original flow:
            // show the regular interstitial and grant the normal reward.
        }

        bool interstitialResolved = false;
        bool interstitialSuccess = false;
        bool interstitialStarted = TryShowInterstitialAd(success =>
        {
            interstitialSuccess = success;
            interstitialResolved = true;
        }, ignoreTemporarySuppression: true, placement: "timed_interstitial");
        // The interstitial request acquires its own pause synchronously, so the
        // warning can release its scope without a single unpaused frame.
        ReleaseCountdownPause();

        if (!interstitialStarted)
        {
            _timedInterstitialFlowInProgress = false;
            yield break;
        }

        while (!interstitialResolved)
            yield return null;

        if (!interstitialSuccess)
        {
            _timedInterstitialFlowInProgress = false;
            yield break;
        }

        GiveTimedReward(baseReward, "timed_reward_free", 1d);
        _timedInterstitialFlowInProgress = false;
    }

    private void AcquireAdPause()
    {
        _adPauseDepth++;
        if (_adPauseDepth != 1)
            return;

        _wasGloballyPausedBeforeAd = G.IsPaused;
        G.IsPaused = true;
        _adPauseManager = PauseManager.Instance;
        if (_adPauseManager != null && !_adPauseManager.IsInitialize)
            _adPauseManager = null;

        if (_adPauseManager != null)
            _adPauseManager.SetPause(true, true);
        else
        {
            _timeScaleBeforeAd = Time.timeScale;
            _audioPauseBeforeAd = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        EnsureAdOverlay();
        if (_adOverlayCanvas != null)
            _adOverlayCanvas.gameObject.SetActive(true);
        if (_adInputBlocker != null)
            _adInputBlocker.gameObject.SetActive(true);
    }

    private void ReleaseAdPause()
    {
        if (_adPauseDepth <= 0)
            return;

        _adPauseDepth--;
        if (_adPauseDepth != 0)
            return;

        G.IsPaused = _wasGloballyPausedBeforeAd;
        if (_adPauseManager != null)
            _adPauseManager.SetPause(false, true);
        else
        {
            Time.timeScale = _timeScaleBeforeAd;
            AudioListener.pause = _audioPauseBeforeAd;
        }

        _adPauseManager = null;
        if (_adInputBlocker != null)
            _adInputBlocker.gameObject.SetActive(false);
        TryHideAdOverlay();
    }

    private bool CanCountTimedInterstitialTime()
    {
        if (AreInterstitialAdsDisabled || _interstitialInProgress || _rewardedInProgress)
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

    private double GiveTimedReward(double baseReward, string source, double multiplier)
    {
        if (baseReward <= 0d)
            return 0d;

        double finalReward = CalculateFinalRewardAmount(baseReward * Math.Max(0d, multiplier));
        if (G.Currency == null)
        {
            Debug.LogError("[AdsManager] Cannot give timed reward: CurrencyManager is not initialized.");
            return 0d;
        }

        double balanceBefore = G.Currency.Coins;
        G.Currency.AddCurrency(CurrencyType.Coins, finalReward);
        Debug.Log($"[AdsManager] Timed reward granted: +{finalReward} coins ({balanceBefore} -> {G.Currency.Coins}), source={source}.");
        GameAnalytics.Track(AnalyticsEventNames.TimedInterstitialRewardGranted, GameAnalytics.Params(
            "placement", "timed_reward",
            "currency_type", "coins",
            "reward_amount", finalReward,
            "base_reward_amount", baseReward,
            "multiplier", multiplier,
            "source", source,
            "result", "success"));

        return finalReward;
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
        if (_adOverlayCanvas == null && !TryCreateAdOverlayFromPrefab())
            CreateRuntimeAdOverlay();

        EnsureAdInputBlocker();
    }

    private void EnsureAdInputBlocker()
    {
        if (_adOverlayCanvas == null || _adInputBlocker != null)
            return;

        Transform existing = _adOverlayCanvas.transform.Find("AdInputBlocker");
        if (existing != null)
            _adInputBlocker = existing.GetComponent<Image>();

        if (_adInputBlocker == null)
        {
            var blockerObject = new GameObject(
                "AdInputBlocker",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            blockerObject.transform.SetParent(_adOverlayCanvas.transform, false);
            _adInputBlocker = blockerObject.GetComponent<Image>();
        }

        RectTransform blockerRect = _adInputBlocker.rectTransform;
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;
        blockerRect.SetAsFirstSibling();
        _adInputBlocker.color = Color.clear;
        _adInputBlocker.raycastTarget = true;
        _adInputBlocker.gameObject.SetActive(_adPauseDepth > 0);
    }

    private bool TryCreateAdOverlayFromPrefab()
    {
        if (adOverlayPrefab == null)
            return false;

        AdsOverlayView view = Instantiate(adOverlayPrefab);
        view.name = "AdsOverlayCanvas";
        view.transform.localScale = Vector3.one;
        DontDestroyOnLoad(view.gameObject);

        _adOverlayCanvas = view.Canvas;
        _countdownPanel = view.CountdownPanel;
        _countdownText = view.CountdownText;
        _countdownRewardText = view.CountdownRewardText;
        _timedRewardX2Button = view.TimedRewardX2Button;
        _timedRewardX2ButtonText = view.TimedRewardX2ButtonText;
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
            _timedRewardX2Button = null;
            _timedRewardX2ButtonText = null;
            _rewardText = null;
            _rewardTextRect = null;
            _usesPrefabOverlayLayout = false;
            _timedRewardX2ButtonInitialized = false;
            return false;
        }

        _usesPrefabOverlayLayout = true;
        _adOverlayCanvas.overrideSorting = true;
        _adOverlayCanvas.sortingOrder = short.MaxValue;
        EnsureTimedRewardX2Button();
        _countdownPanel.gameObject.SetActive(false);
        _rewardText.gameObject.SetActive(false);
        view.gameObject.SetActive(false);
        return true;
    }

    private void CreateRuntimeAdOverlay()
    {
        _usesPrefabOverlayLayout = false;
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
        _countdownPanel.sizeDelta = new Vector2(640f, 330f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;

        _countdownText = CreateOverlayText("CountdownText", _countdownPanel, 54f, Color.white);
        _countdownText.rectTransform.anchorMin = new Vector2(0f, 0.68f);
        _countdownText.rectTransform.anchorMax = new Vector2(1f, 0.97f);
        _countdownText.rectTransform.offsetMin = new Vector2(28f, 0f);
        _countdownText.rectTransform.offsetMax = new Vector2(-28f, -10f);

        _countdownRewardText = CreateOverlayText("CountdownRewardText", _countdownPanel, 34f, new Color(1f, 0.92f, 0.24f, 1f));
        _countdownRewardText.rectTransform.anchorMin = new Vector2(0f, 0.44f);
        _countdownRewardText.rectTransform.anchorMax = new Vector2(1f, 0.65f);
        _countdownRewardText.rectTransform.offsetMin = new Vector2(28f, 6f);
        _countdownRewardText.rectTransform.offsetMax = new Vector2(-28f, 0f);

        _rewardText = CreateOverlayText("InterstitialRewardText", canvasObject.transform, 64f, new Color(1f, 0.92f, 0.24f, 1f));
        _rewardTextRect = _rewardText.rectTransform;
        _rewardTextRect.anchorMin = new Vector2(0.5f, 0.55f);
        _rewardTextRect.anchorMax = new Vector2(0.5f, 0.55f);
        _rewardTextRect.pivot = new Vector2(0.5f, 0.5f);
        _rewardTextRect.sizeDelta = new Vector2(760f, 110f);
        _rewardText.gameObject.SetActive(false);

        EnsureTimedRewardX2Button();
        _countdownPanel.gameObject.SetActive(false);
        canvasObject.SetActive(false);
    }

    private void EnsureTimedRewardX2Button()
    {
        if (_countdownPanel == null || _timedRewardX2ButtonInitialized)
            return;

        Transform existing = _countdownPanel.Find("TimedRewardX2Button");
        if (existing != null)
            _timedRewardX2Button = existing.GetComponent<Button>();

        bool createdAtRuntime = _timedRewardX2Button == null;
        if (createdAtRuntime)
        {
            var buttonObject = new GameObject(
                "TimedRewardX2Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(_countdownPanel, false);
            _timedRewardX2Button = buttonObject.GetComponent<Button>();
        }

        RectTransform buttonRect = _timedRewardX2Button.transform as RectTransform;
        if (createdAtRuntime)
        {
            buttonRect.anchorMin = new Vector2(0.12f, 0.08f);
            buttonRect.anchorMax = new Vector2(0.88f, 0.39f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = Vector2.zero;
            buttonRect.localScale = Vector3.one;
            BlockyUITheme.ApplyButton(_timedRewardX2Button, BlockyUITheme.GreenHeader);
        }

        Transform labelTransform = buttonRect.Find("Label");
        if (labelTransform == null)
        {
            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonRect, false);
            labelTransform = labelObject.transform;
        }

        _timedRewardX2ButtonText = labelTransform.GetComponent<TextMeshProUGUI>();
        if (_timedRewardX2ButtonText == null)
            _timedRewardX2ButtonText = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();

        if (createdAtRuntime)
            TmpUiTextFactory.ApplyDefaults(_timedRewardX2ButtonText);
        RectTransform labelRect = _timedRewardX2ButtonText.rectTransform;
        if (createdAtRuntime)
        {
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(70f, 4f);
            labelRect.offsetMax = new Vector2(-70f, -4f);
        }
        _timedRewardX2ButtonText.text = timedRewardX2ButtonText;
        if (createdAtRuntime)
        {
            _timedRewardX2ButtonText.alignment = TextAlignmentOptions.Center;
            _timedRewardX2ButtonText.enableAutoSizing = true;
            _timedRewardX2ButtonText.fontSizeMin = 24f;
            _timedRewardX2ButtonText.fontSizeMax = 46f;
            _timedRewardX2ButtonText.fontStyle = FontStyles.Bold;
            _timedRewardX2ButtonText.color = Color.white;
            _timedRewardX2ButtonText.raycastTarget = false;
            _timedRewardX2ButtonText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        AdButtonIconDecorator.SetAdIcon(_timedRewardX2Button, true);
        Transform badge = createdAtRuntime ? buttonRect.Find("AdIconBadge") : null;
        if (badge is RectTransform badgeRect)
        {
            badgeRect.anchoredPosition = new Vector2(-10f, -10f);
            badgeRect.sizeDelta = new Vector2(44f, 44f);
        }

        _timedRewardX2Button.gameObject.SetActive(false);
        _timedRewardX2ButtonInitialized = true;
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

    private void ShowCountdown(int seconds, double baseReward, Action onX2)
    {
        EnsureAdOverlay();
        EnsureTimedRewardX2Button();

        _adOverlayCanvas.gameObject.SetActive(true);
        _adOverlayCanvas.transform.localScale = Vector3.one;
        _countdownPanel.gameObject.SetActive(true);

        if (!_usesPrefabOverlayLayout)
        {
            _countdownPanel.sizeDelta = new Vector2(640f, 330f);
            RectTransform titleRect = _countdownText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.68f);
            titleRect.anchorMax = new Vector2(1f, 0.97f);
            titleRect.offsetMin = new Vector2(28f, 0f);
            titleRect.offsetMax = new Vector2(-28f, -10f);
        }
        var countdownFormat = LocalizationUtils.T(
            timedInterstitialCountdownLocalizationKey,
            timedInterstitialCountdownText);
        _countdownText.text = string.Format(CultureInfo.InvariantCulture, countdownFormat, seconds);

        if (_countdownRewardText != null)
        {
            if (!_usesPrefabOverlayLayout)
            {
                RectTransform rewardRect = _countdownRewardText.rectTransform;
                rewardRect.anchorMin = new Vector2(0f, 0.44f);
                rewardRect.anchorMax = new Vector2(1f, 0.65f);
                rewardRect.offsetMin = new Vector2(28f, 6f);
                rewardRect.offsetMax = new Vector2(-28f, 0f);
            }
            UpdateTimedRewardAmount(CalculateFinalRewardAmount(baseReward));
            _countdownRewardText.gameObject.SetActive(true);
        }

        if (_timedRewardX2Button != null)
        {
            _timedRewardX2Button.onClick.RemoveAllListeners();
            if (onX2 != null)
                _timedRewardX2Button.onClick.AddListener(() => onX2.Invoke());
            _timedRewardX2Button.interactable = onX2 != null;
            _timedRewardX2Button.gameObject.SetActive(true);
            AdButtonIconDecorator.SetAdIcon(_timedRewardX2Button, true);
        }

        if (_rewardText != null)
            _rewardText.gameObject.SetActive(false);
    }

    private void UpdateTimedRewardAmount(double amount)
    {
        if (_countdownRewardText == null)
            return;

        var rewardFormat = LocalizationUtils.T(timedInterstitialRewardLocalizationKey, timedInterstitialRewardText);
        _countdownRewardText.text = string.Format(
            CultureInfo.InvariantCulture,
            rewardFormat,
            FormatCoins(amount));
    }

#if UNITY_EDITOR
    [ContextMenu("Ads/Preview 2-1 With X2 Button")]
    public void PreviewTimedInterstitialCountdownForEditor()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AdsManager] Enter Play Mode to preview the ad countdown.");
            return;
        }

        if (_editorCountdownPreviewRoutine != null)
            StopCoroutine(_editorCountdownPreviewRoutine);

        _editorCountdownPreviewRoutine = StartCoroutine(EditorCountdownPreviewRoutine());
    }

    private IEnumerator EditorCountdownPreviewRoutine()
    {
        double baseReward = CalculateTimedInterstitialBaseReward();
        int countdown = InterstitialWarningSeconds;
        for (int i = countdown; i > 0; i--)
        {
            ShowCountdown(i, baseReward, () => { });
            yield return new WaitForSecondsRealtime(1f);
        }

        HideCountdown();
        _editorCountdownPreviewRoutine = null;
    }
#endif

    private void HideCountdown()
    {
        if (_timedRewardX2Button != null)
        {
            _timedRewardX2Button.onClick.RemoveAllListeners();
            _timedRewardX2Button.gameObject.SetActive(false);
        }

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

        if (_adPauseDepth > 0)
        {
            _adOverlayCanvas.gameObject.SetActive(true);
            return;
        }

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

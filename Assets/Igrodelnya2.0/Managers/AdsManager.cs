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

    [Header("Rewarded Ads")]
    [SerializeField] private bool waitForRewardedProvider = true;
    [SerializeField] private float rewardedReadyTimeoutSeconds = 3f;
    [SerializeField] private float rewardedReadyPollSeconds = 0.15f;

    [Header("Timed Interstitial")]
    [SerializeField] private bool timedInterstitialEnabled = true;
    [SerializeField] private float interstitialIntervalSeconds = 60f;
    [SerializeField] private int interstitialCountdownSeconds = 3;
    [SerializeField] private double interstitialIncomeRewardMultiplier = 2d;

    public Action AdClosed;

    private bool _rewardedInProgress;
    private bool _interstitialInProgress;
    private Coroutine _timedInterstitialRoutine;
    private Coroutine _adButtonIconRoutine;
    private Coroutine _rewardPopupRoutine;

    private Canvas _adOverlayCanvas;
    private RectTransform _countdownPanel;
    private TextMeshProUGUI _countdownText;
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
        StartTimedInterstitialRoutine();
        StartAdButtonIconRoutine();
    }

    private void OnDisable()
    {
        SubscribeProviders(false);
    }

    private void OnAdClosed()
    {
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
        if (_rewardedInProgress)
        {
            Debug.LogWarning("Rewarded ad is already in progress.");
            onComplete?.Invoke(false);
            return;
        }

        AdsProvider provider = FindReadyRewardedProvider();
        if (provider != null)
        {
            ShowRewardedAd(provider, rewardId, onComplete);
            return;
        }

        if (!waitForRewardedProvider)
        {
            Debug.LogWarning("No rewarded ads available.");
            onComplete?.Invoke(false);
            return;
        }

        _rewardedInProgress = true;
        StartCoroutine(WaitAndShowRewardedAd(rewardId, onComplete));
    }

    public void ShowInterstitialAd()
    {
        ShowInterstitialAd(null);
    }

    public void ShowInterstitialAd(Action<bool> onComplete)
    {
        if (AreInterstitialAdsDisabled)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (_interstitialInProgress)
        {
            Debug.LogWarning("Interstitial ad is already in progress.");
            onComplete?.Invoke(false);
            return;
        }

        AdsProvider provider = FindReadyInterstitialProvider();
        if (provider == null)
        {
            Debug.LogWarning("No interstitial ads available.");
            onComplete?.Invoke(false);
            return;
        }

        _interstitialInProgress = true;
        provider.ShowInterstitialAd(success =>
        {
            _interstitialInProgress = false;
            onComplete?.Invoke(success);
        });
    }

    public void DisableInterstitialAdsForDays(int days)
    {
        int safeDays = Mathf.Max(1, days);
        long nowUnix = GetCurrentUtcUnixSeconds();
        long currentUntilUnix = Math.Max(GetNoAdsUntilUnix(), nowUnix);
        long until = DateTimeOffset.FromUnixTimeSeconds(currentUntilUnix).AddDays(safeDays).ToUnixTimeSeconds();
        PlayerPrefs.SetString(NoAdsUntilUnixKey, until.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    public void DisableInterstitialAdsForever()
    {
        PlayerPrefs.SetInt(NoAdsForeverKey, 1);
        PlayerPrefs.Save();
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

    private void ShowRewardedAd(AdsProvider provider, string rewardId, Action<bool> onComplete)
    {
        _rewardedInProgress = true;
        provider.ShowRewardedAd(rewardId, success =>
        {
            _rewardedInProgress = false;
            onComplete?.Invoke(success);
        });
    }

    private IEnumerator WaitAndShowRewardedAd(string rewardId, Action<bool> onComplete)
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
            onComplete?.Invoke(false);
            yield break;
        }

        provider.ShowRewardedAd(rewardId, success =>
        {
            _rewardedInProgress = false;
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
        float elapsed = 0f;
        float target = Mathf.Max(5f, interstitialIntervalSeconds);

        while (elapsed < target)
        {
            if (CanCountTimedInterstitialTime())
                elapsed += Time.unscaledDeltaTime;

            yield return null;
        }
    }

    private IEnumerator ShowTimedInterstitialFlow()
    {
        int countdown = Mathf.Max(1, interstitialCountdownSeconds);
        for (int i = countdown; i > 0; i--)
        {
            if (!CanShowTimedInterstitialNow())
            {
                HideCountdown();
                yield break;
            }

            ShowCountdown(i);
            yield return new WaitForSecondsRealtime(1f);
        }

        HideCountdown();

        bool done = false;
        bool shownSuccessfully = false;
        ShowInterstitialAd(success =>
        {
            shownSuccessfully = success;
            done = true;
        });

        while (!done)
            yield return null;

        if (shownSuccessfully)
            GiveTimedInterstitialReward();
    }

    private bool CanCountTimedInterstitialTime()
    {
        if (AreInterstitialAdsDisabled || _interstitialInProgress || _rewardedInProgress)
            return false;

        if (G.Control != null && G.Control.CursorActive)
            return false;

        return true;
    }

    private bool CanShowTimedInterstitialNow()
    {
        if (!CanCountTimedInterstitialTime())
            return false;

        return FindReadyInterstitialProvider() != null;
    }

    private void GiveTimedInterstitialReward()
    {
        double incomePerSecond = CalculateCurrentBaseIncomePerSecond();
        double baseReward = Math.Round(Math.Max(0d, incomePerSecond * Math.Max(0d, interstitialIncomeRewardMultiplier)));
        if (baseReward <= 0d)
            return;

        if (G.Income != null)
            G.Income.AddCoins(baseReward);
        else if (G.Currency != null)
            G.Currency.AddCurrency(CurrencyType.Coins, baseReward);
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
        _countdownPanel.sizeDelta = new Vector2(560f, 180f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;

        _countdownText = CreateOverlayText("CountdownText", _countdownPanel, 54f, Color.white);
        _countdownText.rectTransform.anchorMin = Vector2.zero;
        _countdownText.rectTransform.anchorMax = Vector2.one;
        _countdownText.rectTransform.offsetMin = Vector2.zero;
        _countdownText.rectTransform.offsetMax = Vector2.zero;

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

    private void ShowCountdown(int seconds)
    {
        EnsureAdOverlay();
        _adOverlayCanvas.gameObject.SetActive(true);
        _countdownPanel.gameObject.SetActive(true);
        _countdownText.text = "Реклама через " + seconds.ToString(CultureInfo.InvariantCulture);
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

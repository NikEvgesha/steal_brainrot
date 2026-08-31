using MirraGames.SDK.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShop : MonoBehaviour
{
    public event Action Closed;

    private const string PendingRestoresPrefsKey = "SpecialShop.PendingRestoredPurchases";

    [Serializable]
    private sealed class PendingRestoreState
    {
        public List<string> Ids = new();
    }

    [SerializeField] private List<ShopPackData> _packs;
    [SerializeField] private LocalizationData _localizationData;
    [SerializeField] private GameObject _shopCanvas;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Transform _content;
    [Header("Card Templates - Active")]
    [SerializeField, Tooltip("Half-width card with a Use button for consumable products.")]
    private SpecialShopSlot _smallUseSlotPrefab;
    [SerializeField, Tooltip("Half-width card without a Use button.")]
    private SpecialShopSlot _smallSlotPrefab;
    [SerializeField, Tooltip("Full-width card occupying an entire shop row.")]
    private SpecialShopSlot _wideSlotPrefab;
    [SerializeField, Tooltip("Dedicated Eternal Pack card.")]
    private SpecialShopSlot _eternalPackSlotPrefab;
    [SerializeField] private ShopRow _rowPrefab;
    [SerializeField] private SpecialShopSectionHeader _sectionHeaderPrefab;
    [SerializeField] private int _maxItemsPerRow = 2;
    [SerializeField] private ShopCategory _defaultCategory = ShopCategory.Featured;
    [SerializeField] private List<Button> _categoryButtons = new();
    [SerializeField] private Color _selectedTabColor = new Color(0.2f, 0.84f, 0.08f, 1f);
    [SerializeField] private Color _normalTabColor = new Color(0.04f, 0.46f, 0.9f, 1f);
    [SerializeField] private Sprite _rewardedAdIcon;
    [SerializeField, Min(0.05f)] private float _scrollDuration = 0.3f;
    [Header("Purchase Preview (Editor Only)")]
    [SerializeField, Tooltip("Forces the rewarded-ad fallback even when platform purchases are available.")]
    private bool _forceRewardedAdsForTesting;
    [SerializeField, Tooltip("Shows real-money products without invoking a platform purchase provider.")]
    private bool _forceInAppPurchasesForTesting;
    [SerializeField, Tooltip("Test label displayed on real-money buttons while in-app preview is active.")]
    private string _inAppPreviewPrice = "1.99 USD";

    private readonly Dictionary<string, ShopPackData> _purchaseData = new();
    private readonly Dictionary<string, PurchaseData> _cachedPlatformPurchaseData = new();
    private readonly List<string> _pendingRestoredPurchaseIds = new();
    private readonly HashSet<string> _pendingPurchaseIds = new();
    private readonly List<ShopRow> _rows = new();
    private readonly List<SpecialShopSlot> _slots = new();
    private readonly List<SpecialShopSectionHeader> _sectionHeaders = new();
    private readonly Dictionary<ShopCategory, RectTransform> _sectionAnchors = new();
    private ShopEffectsService _effects;
    private ShopCategory _currentCategory;
    private bool _isOpen;
    private bool _slotsInitialized;
    private bool _slotStateDirty;
    private bool _lastPurchasesAvailable;
    private bool _restorePurchasesRequested;
    private bool _rewardedAdPurchasePending;
    private Coroutine _scrollCoroutine;
    private Coroutine _purchaseCatalogRefreshCoroutine;
    private float _nextPendingRestoreRetry;
    private LocalizationManager _subscribedLocalizationManager;
    private CanvasGroup _shopCanvasGroup;
    private static readonly Vector2 ShopWindowSize = new Vector2(1240f, 820f);
    private const float ShopSafeMargin = 32f;
    private Vector2 _lastShopViewportSize = new Vector2(float.NaN, float.NaN);


    private static readonly ShopCategory[] SectionCategories =
    {
        ShopCategory.Featured,
        ShopCategory.Boosts,
        ShopCategory.Permanent,
        ShopCategory.Currency
    };

    private static readonly ShopCategory[] ButtonCategories =
    {
        ShopCategory.Featured,
        ShopCategory.Boosts,
        ShopCategory.Permanent,
        ShopCategory.Currency
    };

    public bool Opened => _isOpen;
    public ShopCategory CurrentCategory => _currentCategory;
    public ShopEffectsService Effects => _effects;
    public Sprite RewardedAdIcon => _rewardedAdIcon;

    private void Awake()
    {
        if (G.SpecialShop != null && G.SpecialShop != this)
        {
            Debug.LogWarning("[SpecialShop] Duplicate shop removed.");
            Destroy(gameObject);
            return;
        }

        G.SpecialShop = this;
        LoadPendingRestores();
        LocalizationUtils.ConfigureFallback(_localizationData);
        _effects = ShopEffectsService.EnsureExists();
        _effects?.RestoreOwnedPermanentEffects(_packs);
        _currentCategory = _defaultCategory;
        ApplyLayout();
    }

    private void Start()
    {
        _effects?.RestoreOwnedPermanentEffects(_packs);
        // Do not touch the platform purchase SDK during gameplay startup. On
        // WebGL that request can occupy the main thread while the offline reward
        // popup is already visible. The real catalog is refreshed on shop entry.
        // This is a local platform capability check; it does not request the
        // product catalog. It lets us prebuild the correct set of cards.
        _lastPurchasesAvailable = PurchasesAvailable();
        InitSlots(false);
        PrewarmShopCanvas();
    }

    private void Update()
    {
        if (_isOpen)
            UpdateResponsiveWindowScale();

        if (_pendingRestoredPurchaseIds.Count > 0 &&
            _slotsInitialized &&
            Time.unscaledTime >= _nextPendingRestoreRetry)
        {
            _nextPendingRestoreRetry = Time.unscaledTime + 1f;
            FlushPendingRestores();
        }

    }

    private void OnEnable()
    {
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        SubscribeToLocalizationManager(LocalizationManager.Instance);

        if (_effects != null)
            _effects.Changed += OnEffectsChanged;
        if (G.Currency != null)
        {
            G.Currency.NoGems.RemoveListener(OpenCurrency);
            G.Currency.NoGems.AddListener(OpenCurrency);
            G.Currency.CurrencyChanged.RemoveListener(OnCurrencyChanged);
            G.Currency.CurrencyChanged.AddListener(OnCurrencyChanged);
        }
    }

    private void OnDisable()
    {
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        UnsubscribeFromLocalizationManager();

        if (_effects != null)
            _effects.Changed -= OnEffectsChanged;
        if (G.Currency != null)
        {
            G.Currency.NoGems.RemoveListener(OpenCurrency);
            G.Currency.CurrencyChanged.RemoveListener(OnCurrencyChanged);
        }
    }

    private void OnDestroy()
    {
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        UnsubscribeFromLocalizationManager();

        if (G.SpecialShop == this)
            G.SpecialShop = null;
    }

    private void OnCurrencyChanged(CurrencyType currencyType, double _)
    {
        if (_isOpen)
            RefreshSlotAffordability(currencyType);
        else
            _slotStateDirty = true;
    }

    private void OnEffectsChanged()
    {
        if (_isOpen)
            RefreshSlots();
        else
            _slotStateDirty = true;
    }

    private void OnLocalizationManagerReady(LocalizationManager manager)
    {
        SubscribeToLocalizationManager(manager);
    }

    private void SubscribeToLocalizationManager(LocalizationManager manager)
    {
        if (manager == null || manager == _subscribedLocalizationManager)
            return;

        UnsubscribeFromLocalizationManager();
        _subscribedLocalizationManager = manager;
        _subscribedLocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeFromLocalizationManager()
    {
        if (_subscribedLocalizationManager == null)
            return;

        _subscribedLocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        _subscribedLocalizationManager = null;
    }

    private void OnLanguageChanged(string _)
    {
        if (_slotsInitialized)
            InitSlots();
    }

    public void InitSlots(bool refreshPurchaseAvailability = false)
    {
        ClearSlots();
        _slotsInitialized = false;
        _purchaseData.Clear();

        if (_content == null || _smallSlotPrefab == null || _wideSlotPrefab == null ||
            _eternalPackSlotPrefab == null || _rowPrefab == null || _sectionHeaderPrefab == null)
        {
            Debug.LogError("[SpecialShop] Content, one of the four card templates, row or section header prefab is missing.");
            return;
        }

        if (refreshPurchaseAvailability)
            _lastPurchasesAvailable = PurchasesAvailable();
        for (int categoryIndex = 0; categoryIndex < SectionCategories.Length; categoryIndex++)
        {
            ShopCategory category = SectionCategories[categoryIndex];
            var sectionPacks = GetSectionPacks(category);
            if (sectionPacks.Count == 0)
                continue;

            var sectionRows = new List<ShopRow>();
            string previousGroup = null;
            for (int i = 0; i < sectionPacks.Count; i++)
            {
                var item = sectionPacks[i];
                string group = string.IsNullOrWhiteSpace(item.GroupId)
                    ? item.OfferStyle.ToString()
                    : item.GroupId;
                if (!string.Equals(previousGroup, group, StringComparison.Ordinal))
                {
                    var header = Instantiate(_sectionHeaderPrefab, _content);
                    GetOfferGroupTitle(item, out string key, out string fallback);
                    header.Init(key, fallback);
                    _sectionHeaders.Add(header);
                    if (!_sectionAnchors.ContainsKey(category))
                        _sectionAnchors[category] = header.RectTransform;
                    sectionRows.Clear();
                    previousGroup = group;
                }

                Transform row = GetOrCreateAvailableRow(item, sectionRows);
                SpecialShopSlot slotPrefab = ResolveSlotPrefab(item);
                var slot = Instantiate(slotPrefab, row);
                string purchaseId = GetPurchaseId(item);
                PurchaseData data = ResolvePurchaseData(item, purchaseId);

                if (!_purchaseData.ContainsKey(purchaseId))
                    _purchaseData.Add(purchaseId, item);

                slot.Init(this, item, data);
                _slots.Add(slot);
                row.GetComponent<ShopRow>()?.RefreshHeight();
            }
        }

        _slotsInitialized = true;
        _slotStateDirty = false;
        UpdateCategoryButtons();
        FlushPendingRestores();
        Canvas.ForceUpdateCanvases();
    }

    public void ShowFeatured() => ScrollToSection(ShopCategory.Featured);
    public void ShowBoosts() => ScrollToSection(ShopCategory.Boosts);
    public void ShowPermanent() => ScrollToSection(ShopCategory.Permanent);
    public void ShowCurrency() => ScrollToSection(ShopCategory.Currency);

    public void SetCategory(ShopCategory category)
    {
        ScrollToSection(category);
    }

    public void ToggleOpen()
    {
        if (_isOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        OpenAtCategory(_defaultCategory, "shop_button");
    }

    public void OpenCurrency()
    {
        OpenAtCategory(ShopCategory.Currency, "insufficient_currency");
    }

    private void OpenAtCategory(ShopCategory category, string source)
    {
        if (_shopCanvas == null)
            return;

        bool wasOpen = _isOpen;
        if (!wasOpen)
        {
            BeginPurchaseCatalogRefreshOnEntry();
            if (!_restorePurchasesRequested)
            {
                _restorePurchasesRequested = true;
                G.Purchases?.RestorePurchases();
            }
        }

        _isOpen = true;
        SetShopCanvasVisible(true);
        UpdateResponsiveWindowScale(true);
        bool purchasesAvailable = _lastPurchasesAvailable;
        if (!_slotsInitialized)
            InitSlots();
        if (G.Control != null)
            G.Control.CursorActive = true;
        G.Currency?.ShowGems?.Invoke(true);
        G.Input?.AOpenWindow?.Invoke(this);
        if (_slotStateDirty)
            RefreshSlots();
        ScrollToSection(category, false);
        G.Sound?.Play(GameAudioId.SFX_UI_OPEN);
        if (!wasOpen)
        {
            GameAnalytics.Track(AnalyticsEventNames.ShopOpened, GameAnalytics.Params(
                "shop_id", "special_shop",
                "category", category.ToString().ToLowerInvariant(),
                "pack_count", _packs != null ? _packs.Count : 0,
                "purchases_available", purchasesAvailable,
                "source", source,
                "result", "success"),
                AnalyticsPriority.Normal,
                "special_shop_open");
        }
    }

    public void Close()
    {
        if (_shopCanvas == null)
            return;

        bool wasOpen = _isOpen;
        _isOpen = false;
        if (_purchaseCatalogRefreshCoroutine != null)
        {
            StopCoroutine(_purchaseCatalogRefreshCoroutine);
            _purchaseCatalogRefreshCoroutine = null;
        }
        SetShopCanvasVisible(false);
        if (G.Control != null)
            G.Control.CursorActive = false;
        G.Sound?.Play(GameAudioId.SFX_UI_CLOSE);
        if (wasOpen)
            Closed?.Invoke();
    }

    private void PrewarmShopCanvas()
    {
        if (_shopCanvas == null)
            return;

        _isOpen = false;
        _shopCanvas.SetActive(true);
        SetShopCanvasVisible(false);
        UpdateResponsiveWindowScale(true);
        Canvas.ForceUpdateCanvases();
    }

    private void SetShopCanvasVisible(bool visible)
    {
        if (_shopCanvas == null)
            return;

        if (!_shopCanvas.activeSelf)
            _shopCanvas.SetActive(true);

        if (_shopCanvasGroup == null)
        {
            _shopCanvasGroup = _shopCanvas.GetComponent<CanvasGroup>();
            if (_shopCanvasGroup == null)
                _shopCanvasGroup = _shopCanvas.AddComponent<CanvasGroup>();
        }

        _shopCanvasGroup.alpha = visible ? 1f : 0f;
        _shopCanvasGroup.interactable = visible;
        _shopCanvasGroup.blocksRaycasts = visible;
    }

    public void OnPurchaseRestore(string id)
    {
        if (!_slotsInitialized)
        {
            QueuePendingRestore(id);
            return;
        }

        bool granted = GiveReward(id);
        if (granted)
            RemovePendingRestore(id);
        else
            QueuePendingRestore(id);
        GameAnalytics.TrackCritical(AnalyticsEventNames.PurchaseResult, GameAnalytics.Params(
            "product_id", id,
            "pack_id", id,
            "is_restore", true,
            "source", "purchase_restore",
            "result", granted ? "success" : "deferred"),
            "restore:" + id);
    }

    public void TryBuy(PurchaseData purchaseData, ShopPackData packData)
    {
        if (packData == null)
            return;

        if (!RefreshPurchaseStateBeforeBuy(packData, ref purchaseData))
            return;

        GameAnalytics.Track(AnalyticsEventNames.ShopOfferSelected, GameAnalytics.Params(
            "shop_id", "special_shop",
            "pack_id", packData.Id,
            "category", packData.Category.ToString().ToLowerInvariant(),
            "currency_type", packData.PriceCurrencyType.ToString().ToLowerInvariant(),
            "price", packData.Price,
            "rewarded_ad_fallback", IsRewardedAdFallback(packData),
            "source", "shop_pack",
            "result", "selected"));

        if (packData.PriceCurrencyType == CurrencyType.Real && IsInAppPreviewActive())
        {
            Debug.Log($"[SpecialShop] In-app preview only: purchase '{packData.Id}' was not sent.");
            return;
        }

        if (_effects != null && _effects.IsPermanentPackOwned(packData))
        {
            RefreshSlots();
            return;
        }

        if (!IsOfferAvailable(packData) || !CanGrantRewards(packData))
        {
            RefreshSlots();
            return;
        }

        if (IsRewardedAdFallback(packData))
        {
            TryRewardedAdPurchase(packData);
            return;
        }

        if (packData.PriceCurrencyType != CurrencyType.Real)
        {
            if (G.Currency != null &&
                G.Currency.RemoveCurrency(packData.PriceCurrencyType, packData.Price) &&
                !GiveRewards(packData))
            {
                G.Currency.AddCurrency(packData.PriceCurrencyType, packData.Price, playAudio: false);
                Debug.LogError($"[SpecialShop] Reward grant failed for '{packData.Id}'. Currency was refunded.");
            }
            return;
        }

        if (G.Purchases == null ||
            purchaseData == null ||
            string.IsNullOrWhiteSpace(purchaseData.Id) ||
            _pendingPurchaseIds.Contains(purchaseData.Id))
        {
            Debug.LogWarning("[SpecialShop] Purchases are not ready.");
            return;
        }

        string purchaseId = purchaseData.Id;
        _pendingPurchaseIds.Add(purchaseId);
        bool wasPaused = G.IsPaused;
        G.IsPaused = true;
        G.Purchases.BuyPurchase(
            purchaseId,
            success =>
            {
                _pendingPurchaseIds.Remove(purchaseId);
                if (success && !GiveReward(purchaseId))
                    QueuePendingRestore(purchaseId);

                G.IsPaused = wasPaused;
                RefreshSlots();
            });
    }

    public bool TryUse(ShopPackData packData)
    {
        bool result = _effects != null && _effects.TryUseFirstConsumable(packData);
        if (result)
            G.Sound?.Play(GameAudioId.SFX_BOOST_ACTIVATE);
        RefreshSlots();
        return result;
    }

    public bool IsRewardedAdFallback(ShopPackData packData)
    {
        return packData != null
            && packData.PriceCurrencyType == CurrencyType.Real
            && packData.RewardedAdFallback
            && !_lastPurchasesAvailable;
    }

    public bool CanPurchasePack(ShopPackData packData)
    {
        if (packData == null ||
            !IsOfferAvailable(packData) ||
            (_effects != null && _effects.IsPermanentPackOwned(packData)) ||
            _pendingPurchaseIds.Contains(GetPurchaseId(packData)))
        {
            return false;
        }

        if (IsRewardedAdFallback(packData))
            return G.Ad != null && G.Currency != null && !_rewardedAdPurchasePending;

        if (packData.PriceCurrencyType == CurrencyType.Real &&
            !IsInAppPreviewActive() &&
            !_lastPurchasesAvailable)
        {
            return false;
        }

        return CanGrantRewards(packData);
    }

    public bool IsOfferAvailable(ShopPackData packData)
    {
        return GetOfferRemainingSeconds(packData) != 0L;
    }

    public long GetOfferRemainingSeconds(ShopPackData packData)
    {
        if (packData == null || packData.AvailabilityDays <= 0)
            return -1L;

        string groupId = string.IsNullOrWhiteSpace(packData.GroupId)
            ? packData.Id
            : packData.GroupId;
        string key = "SpecialShop.Availability." + groupId + ".Started";
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string raw = PlayerPrefs.GetString(key, string.Empty);
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long started) ||
            started <= 0L ||
            started > now)
        {
            started = now;
            PlayerPrefs.SetString(key, started.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        long ends = started + packData.AvailabilityDays * 86400L;
        return Math.Max(0L, ends - now);
    }

    public string BuildRewardSummary(ShopPackData pack)
    {
        if (pack == null || pack.Rewards == null || pack.Rewards.Count == 0)
            return string.Empty;

        var reward = pack.Rewards[0];
        if (pack.OfferStyle == ShopOfferStyle.LimitedEgg)
        {
            return LocalizationUtils.Format(
                "UI/Shop/LimitedEggBonus",
                "At least {0}% elemental mutation chance",
                Mathf.RoundToInt(Mathf.Max(0f, reward.EffectValue)));
        }

        if (IsRewardedAdFallback(pack))
            return LocalizationUtils.Format("UI/Shop/RewardGems", "+{0} crystals", pack.RewardedAdGems);

        switch (reward.Type)
        {
            case ShopRewardType.Currency:
                return reward.RewardCurrencyType == CurrencyType.Gems
                    ? LocalizationUtils.Format("UI/Shop/RewardGems", "+{0} crystals", reward.Amount)
                    : LocalizationUtils.Format("UI/Shop/RewardCoins", "+{0} coins", reward.Amount);
            case ShopRewardType.NoAdsMonth:
                return LocalizationUtils.Format("UI/Shop/NoAdsDays", "No ads for {0} days", Mathf.Max(1, reward.Amount));
            case ShopRewardType.NoAdsForever:
                return LocalizationUtils.T("UI/Shop/NoAdsForever", "No forced ads forever");
            case ShopRewardType.PermanentIncomePercent:
                return LocalizationUtils.Format("UI/Shop/PermanentIncome", "+{0}% income forever", reward.EffectValue);
            case ShopRewardType.PermanentElementLuckPercent:
                return LocalizationUtils.Format("UI/Shop/PermanentLuck", "+{0}% elemental egg chance forever", reward.EffectValue);
            case ShopRewardType.PermanentElementChanceMultiplier:
                return LocalizationUtils.Format(
                    "UI/Shop/PermanentElementMultiplier",
                    "Elemental egg chance x{0} forever",
                    Mathf.Max(1f, reward.EffectValue).ToString("0.#", CultureInfo.InvariantCulture));
            case ShopRewardType.ConsumableIncomeBoost:
            case ShopRewardType.TimedIncomeBoost:
                return LocalizationUtils.Format("UI/Shop/TimedIncome", "x{0} income for {1} min", reward.EffectValue, reward.DurationMinutes);
            case ShopRewardType.ConsumableElementLuckBoost:
            case ShopRewardType.TimedElementLuckBoost:
                return LocalizationUtils.Format("UI/Shop/TimedLuck", "+{0}% elemental egg chance for {1} min", reward.EffectValue, reward.DurationMinutes);
            case ShopRewardType.ConsumableHatchSkip:
                return LocalizationUtils.Format("UI/Shop/HatchSkip", "-{0} min for all active eggs", reward.DurationMinutes);
            case ShopRewardType.DailyGemsPass:
                return LocalizationUtils.Format(
                    "UI/Shop/DailyGemsPass",
                    "+{0} gems daily for {1} days",
                    reward.Amount,
                    Mathf.Max(1, reward.DurationDays));
            case ShopRewardType.SoftCurrencyHours:
                return LocalizationUtils.Format(
                    "UI/Shop/SoftCurrencyHours",
                    "{0} hours of income — {1} now",
                    Mathf.Max(1, reward.DurationMinutes / 60),
                    FormatAmount(CalculateSoftCurrencyReward(reward)));
            case ShopRewardType.PermanentOfflineIncomeMultiplier:
                return LocalizationUtils.Format(
                    "UI/Shop/PermanentOfflineIncome",
                    "Offline income x{0} forever",
                    Mathf.Max(1f, reward.EffectValue).ToString("0.#", CultureInfo.InvariantCulture));
            case ShopRewardType.PermanentHatchSpeedPercent:
                return LocalizationUtils.Format(
                    "UI/Shop/PermanentHatchSpeed",
                    "+{0}% hatch speed forever",
                    reward.EffectValue);
            case ShopRewardType.ConsumableHatchSpeedBoost:
                return LocalizationUtils.Format(
                    "UI/Shop/TimedHatchSpeed",
                    "+{0}% hatch speed for {1} min",
                    reward.EffectValue,
                    reward.DurationMinutes);
            case ShopRewardType.ConsumableOmniBoost:
                return LocalizationUtils.Format(
                    "UI/Shop/OmniPotion",
                    "Luck, coins and hatching boosted for {0} min",
                    reward.DurationMinutes);
            case ShopRewardType.InstantHatchAll:
                return LocalizationUtils.T(
                    "UI/Shop/InstantHatchAll",
                    "Instantly hatches all active eggs");
            case ShopRewardType.Item:
                return LocalizationUtils.Format("UI/Shop/ItemAmount", "Item x{0}", Mathf.Max(1, reward.Amount));
            default:
                return string.Empty;
        }
    }

    public string BuildPackTitle(ShopPackData pack)
    {
        if (pack == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(pack.Name) &&
            pack.Name.StartsWith("UI/", StringComparison.Ordinal))
        {
            return LocalizationUtils.T(pack.Name, pack.Name);
        }

        if (pack.OfferStyle == ShopOfferStyle.EternalPack)
            return LocalizationUtils.T("UI/Shop/Title/EternalPack", "Eternal pack");

        if (!TryGetPrimaryReward(pack, out ShopReward reward))
            return pack.Name ?? string.Empty;

        string effect = Mathf.Max(0f, reward.EffectValue)
            .ToString("0.#", CultureInfo.InvariantCulture);
        switch (pack.OfferStyle)
        {
            case ShopOfferStyle.LimitedEgg:
                return LocalizationUtils.Format(
                    "UI/Shop/Title/LimitedEgg",
                    "{0} limited eggs",
                    Mathf.Max(1, reward.Amount));
            case ShopOfferStyle.MonthlyPass:
                return LocalizationUtils.Format(
                    "UI/Shop/Title/MonthlyPass",
                    "{0}-day pack",
                    ResolvePassDays(pack));
            case ShopOfferStyle.TimedIncome:
                return LocalizationUtils.Format("UI/Shop/Title/TimedIncome", "Income x{0}", effect);
            case ShopOfferStyle.TimedLuck:
                return LocalizationUtils.Format("UI/Shop/Title/TimedLuck", "+{0}% luck", effect);
            case ShopOfferStyle.PremiumCurrency:
                return LocalizationUtils.Format(
                    "UI/Shop/Title/Gems",
                    "{0} gems",
                    Mathf.Max(0, reward.Amount).ToString("N0", CultureInfo.InvariantCulture));
            case ShopOfferStyle.SoftCurrency:
                return LocalizationUtils.Format(
                    "UI/Shop/Title/SoftCurrency",
                    "{0} hours of income",
                    Mathf.Max(1, reward.DurationMinutes / 60));
            case ShopOfferStyle.Permanent:
                return BuildPermanentPackTitle(reward, effect);
            case ShopOfferStyle.Potion:
                return BuildPotionPackTitle(reward);
            default:
                return pack.Name ?? string.Empty;
        }
    }

    public string BuildPackDescription(ShopPackData pack)
    {
        if (pack == null)
            return string.Empty;

        if (pack.OfferStyle == ShopOfferStyle.MonthlyPass)
        {
            return LocalizationUtils.Format(
                "UI/Shop/Description/MonthlyPass",
                "No forced ads, +{0} gems daily for {1} days.",
                ResolveDailyGems(pack),
                ResolvePassDays(pack));
        }

        if (pack.OfferStyle != ShopOfferStyle.Standard)
            return string.Empty;

        return LocalizationUtils.T(pack.DescriptionKey, pack.DescriptionFallback);
    }

    public string BuildPackBadge(ShopPackData pack)
    {
        if (pack == null)
            return string.Empty;

        string raw = pack.BadgeFallback ?? string.Empty;
        if (raw.Contains("%", StringComparison.Ordinal))
            return raw;

        TryGetPrimaryReward(pack, out ShopReward reward);
        switch (pack.OfferStyle)
        {
            case ShopOfferStyle.TimedIncome:
            case ShopOfferStyle.TimedLuck:
                return LocalizationUtils.Format(
                    "UI/Shop/Badge/Minutes",
                    "{0} MIN",
                    Mathf.Max(1, reward.DurationMinutes));
            case ShopOfferStyle.MonthlyPass:
            {
                int days = ResolvePassDays(pack);
                if (days >= 90)
                    return LocalizationUtils.T("UI/Shop/Badge/Maximum", "MAXIMUM");
                if (days >= 60)
                    return LocalizationUtils.T("UI/Shop/Badge/Value", "GREAT VALUE");
                return LocalizationUtils.Format(
                    "UI/Shop/Badge/Daily",
                    "+{0} DAILY",
                    ResolveDailyGems(pack));
            }
            case ShopOfferStyle.EternalPack:
                return LocalizationUtils.T("UI/Shop/Badge/DailyReset", "DAILY RESET");
            case ShopOfferStyle.Permanent:
                return pack.Id != null &&
                       pack.Id.Contains("luck", StringComparison.OrdinalIgnoreCase)
                    ? LocalizationUtils.T("UI/Shop/Badge/Ultra", "ULTRA")
                    : LocalizationUtils.T("UI/Shop/Badge/Forever", "FOREVER");
            case ShopOfferStyle.PremiumCurrency:
                if (reward.Amount >= 2000)
                    return LocalizationUtils.T("UI/Shop/Badge/BestValue", "BEST VALUE");
                if (reward.Amount <= 100)
                    return LocalizationUtils.T("UI/Shop/Badge/Start", "START");
                return raw;
            case ShopOfferStyle.SoftCurrency:
                return LocalizationUtils.Format(
                    "UI/Shop/Badge/Hours",
                    "{0} HOURS",
                    Mathf.Max(1, reward.DurationMinutes / 60));
            case ShopOfferStyle.Potion:
                return reward.Type == ShopRewardType.InstantHatchAll
                    ? LocalizationUtils.T("UI/Shop/Badge/OneClick", "ONE CLICK")
                    : LocalizationUtils.T("UI/Shop/Badge/Potion", "POTION");
            case ShopOfferStyle.LimitedEgg:
                return reward.Amount >= 10
                    ? LocalizationUtils.T("UI/Shop/Badge/BestValue", "BEST VALUE")
                    : LocalizationUtils.Format(
                        "UI/Shop/Badge/LimitedDays",
                        "LIMITED {0}D",
                        Mathf.Max(1, pack.AvailabilityDays));
            default:
                return LocalizationUtils.T(raw, raw);
        }
    }

    private static string BuildPermanentPackTitle(ShopReward reward, string effect)
    {
        switch (reward.Type)
        {
            case ShopRewardType.PermanentElementChanceMultiplier:
                return LocalizationUtils.Format("UI/Shop/Title/Mutations", "Mutations x{0}", effect);
            case ShopRewardType.PermanentOfflineIncomeMultiplier:
                return LocalizationUtils.Format("UI/Shop/Title/OfflineIncome", "Offline income x{0}", effect);
            case ShopRewardType.PermanentIncomePercent:
                return LocalizationUtils.Format("UI/Shop/Title/PermanentIncome", "+{0}% coins", effect);
            case ShopRewardType.PermanentElementLuckPercent:
                return LocalizationUtils.Format("UI/Shop/Title/PermanentLuck", "+{0}% luck", effect);
            case ShopRewardType.PermanentHatchSpeedPercent:
                return LocalizationUtils.Format("UI/Shop/Title/PermanentHatch", "+{0}% hatch speed", effect);
            default:
                return LocalizationUtils.T("UI/Shop/Group/Permanent", "Permanent upgrades");
        }
    }

    private static string BuildPotionPackTitle(ShopReward reward)
    {
        switch (reward.Type)
        {
            case ShopRewardType.ConsumableIncomeBoost:
                return LocalizationUtils.T("UI/Shop/Title/PotionIncome", "Coin potion");
            case ShopRewardType.ConsumableElementLuckBoost:
                return LocalizationUtils.T("UI/Shop/Title/PotionLuck", "Luck potion");
            case ShopRewardType.ConsumableHatchSpeedBoost:
                return LocalizationUtils.T("UI/Shop/Title/PotionHatch", "Hatch potion");
            case ShopRewardType.ConsumableOmniBoost:
                return LocalizationUtils.T("UI/Shop/Title/PotionOmni", "Omni potion");
            case ShopRewardType.InstantHatchAll:
                return LocalizationUtils.T("UI/Shop/Title/InstantHatch", "Instant hatch");
            default:
                return LocalizationUtils.T("UI/Shop/Badge/Potion", "Potion");
        }
    }

    private static bool TryGetPrimaryReward(ShopPackData pack, out ShopReward reward)
    {
        reward = default;
        if (pack == null || pack.Rewards == null || pack.Rewards.Count == 0)
            return false;

        reward = pack.Rewards[0];
        return true;
    }

    private static int ResolvePassDays(ShopPackData pack)
    {
        if (pack != null && pack.Rewards != null)
        {
            for (int i = 0; i < pack.Rewards.Count; i++)
            {
                ShopReward reward = pack.Rewards[i];
                if (reward.Type == ShopRewardType.NoAdsMonth)
                    return Mathf.Max(1, Mathf.Max(reward.DurationDays, reward.Amount));
            }
        }

        return 30;
    }

    private static int ResolveDailyGems(ShopPackData pack)
    {
        if (pack != null && pack.Rewards != null)
        {
            for (int i = 0; i < pack.Rewards.Count; i++)
            {
                if (pack.Rewards[i].Type == ShopRewardType.DailyGemsPass)
                    return Mathf.Max(0, pack.Rewards[i].Amount);
            }
        }

        return 0;
    }

    private List<ShopPackData> GetSectionPacks(ShopCategory category)
    {
        var result = new List<ShopPackData>();
        if (_packs == null)
            return result;

        for (int i = 0; i < _packs.Count; i++)
        {
            var pack = _packs[i];
            if (pack == null)
                continue;

            if (pack.Category != category)
                continue;
            if (!IsOfferAvailable(pack))
                continue;
            if (pack.PriceCurrencyType == CurrencyType.Real && !_lastPurchasesAvailable && !pack.RewardedAdFallback)
                continue;

            result.Add(pack);
        }

        result.Sort((a, b) =>
        {
            int sort = a.SortOrder.CompareTo(b.SortOrder);
            return sort != 0 ? sort : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        });
        return result;
    }

    private Transform GetOrCreateAvailableRow(ShopPackData pack, List<ShopRow> sectionRows)
    {
        int requestedItems = pack != null ? pack.ItemsPerRow : _maxItemsPerRow;
        int itemsPerRow = Mathf.Clamp(requestedItems, 1, Mathf.Max(1, _maxItemsPerRow));
        if (itemsPerRow > 1)
        {
            for (int i = 0; i < sectionRows.Count; i++)
            {
                if (sectionRows[i] != null &&
                    sectionRows[i].MaxItems == itemsPerRow &&
                    sectionRows[i].ItemsCount < sectionRows[i].MaxItems)
                {
                    return sectionRows[i].transform;
                }
            }
        }

        var newRow = Instantiate(_rowPrefab, _content);
        newRow.setMaxItems(itemsPerRow);
        sectionRows.Add(newRow);
        _rows.Add(newRow);
        return newRow.transform;
    }

    private PurchaseData ResolvePurchaseData(ShopPackData pack, string purchaseId)
    {
        if (pack == null || pack.PriceCurrencyType != CurrencyType.Real || !_lastPurchasesAvailable)
            return PurchaseData.Fallback(purchaseId);

        if (IsInAppPreviewActive())
            return new PurchaseData(
                purchaseId,
                string.Empty,
                string.Empty,
                string.IsNullOrWhiteSpace(_inAppPreviewPrice)
                    ? pack.Price.ToString(CultureInfo.InvariantCulture)
                    : _inAppPreviewPrice,
                string.Empty);

        if (_cachedPlatformPurchaseData.TryGetValue(purchaseId, out PurchaseData cachedData))
            return cachedData;

        return PurchaseData.Fallback(purchaseId);
    }

    private SpecialShopSlot ResolveSlotPrefab(ShopPackData pack)
    {
        if (pack == null)
            return _smallSlotPrefab != null ? _smallSlotPrefab : _wideSlotPrefab;
        if (pack.OfferStyle == ShopOfferStyle.EternalPack)
            return _eternalPackSlotPrefab;
        if (pack.ItemsPerRow <= 1)
            return _wideSlotPrefab;
        if (pack.HasConsumableReward && _smallUseSlotPrefab != null)
            return _smallUseSlotPrefab;
        return _smallSlotPrefab;
    }

    private void BeginPurchaseCatalogRefreshOnEntry()
    {
        bool purchasesAvailable = PurchasesAvailable();
        bool availabilityChanged = purchasesAvailable != _lastPurchasesAvailable;
        _lastPurchasesAvailable = purchasesAvailable;

        if (!purchasesAvailable || G.Purchases == null)
            _cachedPlatformPurchaseData.Clear();

        if (!_slotsInitialized || availabilityChanged)
            InitSlots(false);

        if (!purchasesAvailable || G.Purchases == null)
            return;

        if (_purchaseCatalogRefreshCoroutine != null)
            StopCoroutine(_purchaseCatalogRefreshCoroutine);
        _purchaseCatalogRefreshCoroutine = StartCoroutine(RefreshPurchaseCatalogOnEntryRoutine());
    }

    private IEnumerator RefreshPurchaseCatalogOnEntryRoutine()
    {
        if (_packs == null || G.Purchases == null)
            yield break;

        // Open and render the window first; querying Mirra in the same frame is
        // exactly the hitch this routine is intended to avoid.
        yield return null;

        var refreshedIds = new HashSet<string>();
        for (int i = 0; i < _packs.Count; i++)
        {
            if (!_isOpen)
                break;

            ShopPackData pack = _packs[i];
            if (pack == null || pack.PriceCurrencyType != CurrencyType.Real || !IsOfferAvailable(pack))
                continue;

            string purchaseId = GetPurchaseId(pack);
            if (!refreshedIds.Add(purchaseId))
                continue;

            PurchaseData purchaseData = G.Purchases.GetPurchaseData(purchaseId);
            if (purchaseData != null && !string.IsNullOrWhiteSpace(purchaseData.Id))
            {
                _cachedPlatformPurchaseData[purchaseId] = purchaseData;

                for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
                {
                    SpecialShopSlot slot = _slots[slotIndex];
                    ShopPackData slotPack = slot != null ? slot.PackData : null;
                    if (slotPack != null && string.Equals(GetPurchaseId(slotPack), purchaseId, StringComparison.Ordinal))
                        slot.RefreshPurchaseData(purchaseData);
                }
            }

            // Mirra obtains price and currency through synchronous WebGL calls.
            // Spreading products over frames prevents a large hitch on shop open.
            yield return null;
        }

        _purchaseCatalogRefreshCoroutine = null;
    }

    private bool RefreshPurchaseStateBeforeBuy(
        ShopPackData pack,
        ref PurchaseData purchaseData)
    {
        if (pack == null || pack.PriceCurrencyType != CurrencyType.Real || IsInAppPreviewActive())
            return true;

        bool purchasesAvailable = PurchasesAvailable();
        bool availabilityChanged = purchasesAvailable != _lastPurchasesAvailable;
        _lastPurchasesAvailable = purchasesAvailable;
        if (availabilityChanged)
            InitSlots(false);

        if (!purchasesAvailable)
            return pack.RewardedAdFallback;
        if (G.Purchases == null)
            return false;

        string purchaseId = GetPurchaseId(pack);
        purchaseData = G.Purchases.GetPurchaseData(purchaseId);
        if (purchaseData != null && !string.IsNullOrWhiteSpace(purchaseData.Id))
            _cachedPlatformPurchaseData[purchaseId] = purchaseData;
        return purchaseData != null && !string.IsNullOrWhiteSpace(purchaseData.Id);
    }

    private void ScrollToSection(ShopCategory category, bool animated = true)
    {
        if (!_slotsInitialized || _scrollRect == null || !_sectionAnchors.ContainsKey(category))
            return;

        _currentCategory = category;
        UpdateCategoryButtons();

        if (_scrollCoroutine != null)
            StopCoroutine(_scrollCoroutine);
        _scrollCoroutine = StartCoroutine(ScrollToSectionRoutine(category, animated));
    }

    private IEnumerator ScrollToSectionRoutine(ShopCategory category, bool animated)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        float target = 1f;
        if (_sectionAnchors.TryGetValue(category, out var anchor) && anchor != null)
        {
            float contentHeight = (_content as RectTransform)?.rect.height ?? 0f;
            float viewportHeight = _scrollRect.viewport != null ? _scrollRect.viewport.rect.height : 0f;
            float scrollableHeight = Mathf.Max(0f, contentHeight - viewportHeight);
            if (scrollableHeight > 0.01f)
            {
                float sectionTop = Mathf.Max(0f, -anchor.anchoredPosition.y - anchor.rect.height * (1f - anchor.pivot.y));
                target = 1f - Mathf.Clamp01(sectionTop / scrollableHeight);
            }
        }

        if (!animated)
        {
            _scrollRect.verticalNormalizedPosition = target;
            _scrollCoroutine = null;
            yield break;
        }

        float start = _scrollRect.verticalNormalizedPosition;
        float elapsed = 0f;
        while (elapsed < _scrollDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _scrollDuration));
            _scrollRect.verticalNormalizedPosition = Mathf.Lerp(start, target, t);
            yield return null;
        }

        _scrollRect.verticalNormalizedPosition = target;
        _scrollCoroutine = null;
    }

    private void TryRewardedAdPurchase(ShopPackData packData)
    {
        if (_rewardedAdPurchasePending || G.Ad == null)
            return;

        _rewardedAdPurchasePending = true;
        G.Ad.ShowRewardedAd("SpecialShopGems", success =>
        {
            _rewardedAdPurchasePending = false;
            if (!success)
                return;

            G.Sound?.Play(GameAudioId.SFX_AD_SUCCESS);
            G.Currency?.AddCurrency(CurrencyType.Gems, packData.RewardedAdGems);
            RefreshSlots();
        });
    }

    private bool PurchasesAvailable()
    {
        if (_forceRewardedAdsForTesting)
            return false;
        if (IsInAppPreviewActive())
            return true;
        return G.Purchases != null && G.Purchases.PurchasesAvailable();
    }

    private bool IsInAppPreviewActive()
    {
#if UNITY_EDITOR
        return _forceInAppPurchasesForTesting && !_forceRewardedAdsForTesting;
#else
        return false;
#endif
    }

    [ContextMenu("Preview/Use Platform Mode")]
    private void UsePlatformPurchasePreview()
    {
        _forceRewardedAdsForTesting = false;
        _forceInAppPurchasesForTesting = false;
        RefreshPurchasePreview();
    }

    [ContextMenu("Preview/Force Rewarded Ads")]
    private void UseRewardedAdPreview()
    {
        _forceRewardedAdsForTesting = true;
        _forceInAppPurchasesForTesting = false;
        RefreshPurchasePreview();
    }

    [ContextMenu("Preview/Force In-App Purchases")]
    private void UseInAppPurchasePreview()
    {
        _forceRewardedAdsForTesting = false;
        _forceInAppPurchasesForTesting = true;
        RefreshPurchasePreview();
    }

    private void RefreshPurchasePreview()
    {
        if (Application.isPlaying)
            InitSlots();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private static void GetSectionTitle(ShopCategory category, out string key, out string fallback)
    {
        switch (category)
        {
            case ShopCategory.Boosts:
                key = "UI/Shop/SectionBoosts";
                fallback = "Boosts";
                break;
            case ShopCategory.Permanent:
                key = "UI/Shop/SectionPermanent";
                fallback = "Permanent";
                break;
            case ShopCategory.Currency:
                key = "UI/Shop/SectionCurrency";
                fallback = "Gems";
                break;
            default:
                key = "UI/Shop/SectionFeatured";
                fallback = "Featured";
                break;
        }
    }

    private static void GetOfferGroupTitle(ShopPackData pack, out string key, out string fallback)
    {
        if (pack == null)
        {
            key = "UI/Shop/Section";
            fallback = "Offers";
            return;
        }

        switch (pack.OfferStyle)
        {
            case ShopOfferStyle.LimitedEgg:
                key = "UI/Shop/Group/LimitedEgg";
                fallback = "Limited egg — 21 days left";
                break;
            case ShopOfferStyle.MonthlyPass:
                key = "UI/Shop/Group/MonthlyPass";
                fallback = "Premium packs";
                break;
            case ShopOfferStyle.EternalPack:
                key = "UI/Shop/Group/EternalPack";
                fallback = "Daily reward track";
                break;
            case ShopOfferStyle.TimedIncome:
                key = "UI/Shop/Group/TimedIncome";
                fallback = "Income boosters";
                break;
            case ShopOfferStyle.TimedLuck:
                key = "UI/Shop/Group/TimedLuck";
                fallback = "Luck boosters";
                break;
            case ShopOfferStyle.Potion:
                key = "UI/Shop/Group/Potions";
                fallback = "Potions";
                break;
            case ShopOfferStyle.Permanent:
                key = "UI/Shop/Group/Permanent";
                fallback = "Permanent upgrades";
                break;
            case ShopOfferStyle.PremiumCurrency:
                key = "UI/Shop/Group/PremiumCurrency";
                fallback = "Gems";
                break;
            case ShopOfferStyle.SoftCurrency:
                key = "UI/Shop/Group/SoftCurrency";
                fallback = "Coins by income time";
                break;
            default:
                GetSectionTitle(pack.Category, out key, out fallback);
                break;
        }
    }

    private bool CanGrantRewards(ShopPackData packData)
    {
        if (packData == null || packData.Rewards == null || packData.Rewards.Count == 0)
            return false;

        for (int i = 0; i < packData.Rewards.Count; i++)
        {
            if (!CanGrantReward(packData.Rewards[i]))
                return false;
        }

        return true;
    }

    private bool CanGrantReward(ShopReward reward)
    {
        switch (reward.Type)
        {
            case ShopRewardType.Item:
                return reward.Item != null &&
                       G.Inventory != null &&
                       G.Inventory.IsInitialized &&
                       G.Player != null &&
                       G.QuickAccess != null;
            case ShopRewardType.Currency:
                return G.Currency != null && reward.Amount > 0;
            case ShopRewardType.SoftCurrencyHours:
                return G.Currency != null && CalculateSoftCurrencyReward(reward) > 0d;
            case ShopRewardType.NoAdsMonth:
            case ShopRewardType.NoAdsForever:
                return G.Ad != null;
            default:
                return _effects != null;
        }
    }

    private bool GiveReward(string purchaseId)
    {
        if (string.IsNullOrWhiteSpace(purchaseId) || !_purchaseData.TryGetValue(purchaseId, out var packData))
        {
            string packId = purchaseId != null && purchaseId.StartsWith("Pack_", StringComparison.Ordinal)
                ? purchaseId.Substring(5)
                : purchaseId;
            packData = _packs?.Find(x => x != null && string.Equals(x.Id, packId, StringComparison.Ordinal));
        }

        return packData != null && GiveRewards(packData);
    }

    private bool GiveRewards(ShopPackData packData)
    {
        if (!CanGrantRewards(packData))
            return false;

        using (GameAnalytics.BeginItemGrant(
                   "special_shop",
                   packData.Id,
                   packData.PriceCurrencyType.ToString().ToLowerInvariant(),
                   packData.Price))
        {
            for (int rewardIndex = 0; rewardIndex < packData.Rewards.Count; rewardIndex++)
            {
                if (!GiveSingleReward(packData.Id, packData.Rewards[rewardIndex]))
                    return false;
            }
        }

        var majorPurchase =
            packData.PriceCurrencyType == CurrencyType.Real ||
            packData.Category == ShopCategory.Permanent;
        G.Sound?.Play(majorPurchase ? GameAudioId.SFX_SHOP_PURCHASE_MAJOR : GameAudioId.SFX_SHOP_PURCHASE);
        RefreshSlots();
        return true;
    }

    public int GetEternalPackStep(ShopPackData pack)
    {
        if (pack == null)
            return 0;

        ResetEternalPackIfNeeded(pack);
        return Mathf.Max(0, PlayerPrefs.GetInt(BuildEternalPackKey(pack.Id, "Step"), 0));
    }

    public bool TryAdvanceEternalPack(ShopPackData pack)
    {
        if (pack == null || pack.TrackSteps == null || pack.TrackSteps.Count == 0)
            return false;

        ResetEternalPackIfNeeded(pack);
        int stepIndex = GetEternalPackStep(pack);
        if (stepIndex < 0 || stepIndex >= pack.TrackSteps.Count)
            return false;

        ShopTrackStep step = pack.TrackSteps[stepIndex];
        if (!CanGrantReward(step.Reward))
            return false;

        bool currencySpent = false;
        if (!step.Free)
        {
            if (step.PriceCurrencyType == CurrencyType.Real)
            {
                Debug.LogWarning("[SpecialShop] Eternal-pack real-money gates require a configured platform SKU.");
                return false;
            }

            if (G.Currency == null ||
                !G.Currency.RemoveCurrency(
                    step.PriceCurrencyType,
                    step.Price,
                    "eternal_pack_" + pack.Id))
            {
                return false;
            }

            currencySpent = true;
        }

        bool granted;
        using (GameAnalytics.BeginItemGrant("eternal_pack", pack.Id + ":" + stepIndex))
            granted = GiveSingleReward(pack.Id + ".track." + stepIndex, step.Reward);
        if (!granted)
        {
            if (currencySpent)
                G.Currency?.AddCurrency(step.PriceCurrencyType, step.Price, playAudio: false);
            return false;
        }

        PlayerPrefs.SetInt(BuildEternalPackKey(pack.Id, "Step"), stepIndex + 1);
        PlayerPrefs.Save();
        G.Sound?.Play(step.Free ? GameAudioId.SFX_REWARD_CLAIM : GameAudioId.SFX_SHOP_PURCHASE_MAJOR);
        RefreshSlots();
        return true;
    }

    public double CalculateSoftCurrencyReward(ShopReward reward)
    {
        double seconds = Mathf.Max(1, reward.DurationMinutes) * 60d;
        double incomePerSecond = CalculateCurrentBaseIncomePerSecond();
        double finalIncomePerSecond = G.Income != null
            ? G.Income.Apply(incomePerSecond)
            : incomePerSecond;
        return Math.Max(0d, Math.Round(finalIncomePerSecond * seconds));
    }

    public string FormatAmount(double amount)
    {
        double value = Math.Max(0d, amount);
        string[] suffixes = { string.Empty, "K", "M", "B", "T", "a", "b", "c", "d" };
        int suffix = 0;
        while (value >= 1000d && suffix < suffixes.Length - 1)
        {
            value /= 1000d;
            suffix++;
        }

        string format = value >= 100d ? "0" : value >= 10d ? "0.#" : "0.##";
        return value.ToString(format, CultureInfo.InvariantCulture) + suffixes[suffix];
    }

    private bool GiveSingleReward(string sourceId, ShopReward reward)
    {
        switch (reward.Type)
        {
            case ShopRewardType.Item:
                if (!CanGrantReward(reward))
                    return false;
                for (int i = 0; i < Mathf.Max(1, reward.Amount); i++)
                {
                    InventoryItem item = Instantiate(reward.Item);
                    if (item is Egg egg)
                    {
                        if (reward.EffectValue > 0f)
                            egg.SetRandomData(reward.EffectValue / 100f);
                        else
                            egg.SetRandomData();
                    }

                    G.Inventory.Add(item);
                }
                return true;
            case ShopRewardType.Currency:
                if (G.Currency == null)
                    return false;
                G.Currency.AddCurrency(reward.RewardCurrencyType, reward.Amount);
                return true;
            case ShopRewardType.SoftCurrencyHours:
                double amount = CalculateSoftCurrencyReward(reward);
                if (G.Currency == null || amount <= 0d)
                    return false;
                G.Currency.AddCurrency(CurrencyType.Coins, amount);
                return true;
            case ShopRewardType.NoAdsMonth:
                if (G.Ad == null)
                    return false;
                G.Ad.DisableInterstitialAdsForDays(
                    reward.DurationDays > 0
                        ? reward.DurationDays
                        : reward.Amount > 0 ? reward.Amount : 30);
                return true;
            case ShopRewardType.NoAdsForever:
                if (G.Ad == null)
                    return false;
                G.Ad.DisableInterstitialAdsForever();
                return true;
            default:
                return _effects != null && _effects.GrantReward(sourceId, reward);
        }
    }

    private static double CalculateCurrentBaseIncomePerSecond()
    {
        double total = 0d;
        FieldCell[] cells = FindObjectsByType<FieldCell>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
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

        BigPetPoint[] bigPets = FindObjectsByType<BigPetPoint>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < bigPets.Length; i++)
        {
            BigPetPoint bigPet = bigPets[i];
            if (bigPet == null || bigPet.IsRemoteMode)
                continue;
            total += Math.Max(0d, bigPet.CurrentIncomePerSecond);
        }

        return total;
    }

    private static void ResetEternalPackIfNeeded(ShopPackData pack)
    {
        string today = DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string dateKey = BuildEternalPackKey(pack.Id, "Date");
        if (string.Equals(PlayerPrefs.GetString(dateKey, string.Empty), today, StringComparison.Ordinal))
            return;

        PlayerPrefs.SetString(dateKey, today);
        PlayerPrefs.SetInt(BuildEternalPackKey(pack.Id, "Step"), 0);
        PlayerPrefs.Save();
    }

    private static string BuildEternalPackKey(string packId, string suffix)
    {
        return "SpecialShop.Eternal." + packId + "." + suffix;
    }

    private void ClearSlots()
    {
        if (_content != null)
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                if (child.GetComponent<ShopRow>() == null
                    && child.GetComponent<SpecialShopSectionHeader>() == null)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        _rows.Clear();
        _slots.Clear();
        _sectionHeaders.Clear();
        _sectionAnchors.Clear();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i]?.RefreshState();

        _slotStateDirty = false;
    }

    private void RefreshSlotAffordability(CurrencyType currencyType)
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i]?.RefreshAffordability(currencyType);
    }

    private void UpdateCategoryButtons()
    {
        for (int i = 0; i < _categoryButtons.Count; i++)
        {
            var button = _categoryButtons[i];
            if (button == null)
                continue;

            ShopCategory category = i < ButtonCategories.Length ? ButtonCategories[i] : (ShopCategory)i;
            bool available = GetSectionPacks(category).Count > 0;
            button.gameObject.SetActive(available);
            button.interactable = available;
            if (!available)
                continue;

            if (button.targetGraphic != null)
                button.targetGraphic.color = category == _currentCategory ? _selectedTabColor : _normalTabColor;

            Transform labelTransform = button.transform.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
            if (label != null)
            {
                GetSectionTitle(category, out string key, out string fallback);
                label.text = LocalizationUtils.T(key, fallback);
            }
        }
    }

    [ContextMenu("Apply shop layout")]
    public void ApplyLayout()
    {
        if (_shopCanvas == null)
            return;

        var dim = _shopCanvas.transform.Find("Dim")?.GetComponent<Image>();
        if (dim != null)
            dim.color = new Color(0f, 0f, 0f, 0.76f);

        Transform window = _shopCanvas.transform.Find("Window");
        if (window == null)
            return;

        if (window is RectTransform windowRect)
        {
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = ShopWindowSize;
            UpdateResponsiveWindowScale(windowRect, true);
        }

        Transform header = window.Find("Header");
        if (header is RectTransform headerRect)
        {
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 104f);

            if (header.Find("Title") is RectTransform titleRect)
            {
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = new Vector2(24f, 2f);
                titleRect.offsetMax = new Vector2(-104f, -2f);
                var title = titleRect.GetComponent<TextMeshProUGUI>();
                if (title != null)
                {
                    title.color = Color.white;
                    title.alignment = TextAlignmentOptions.MidlineLeft;
                    title.fontStyle = FontStyles.Bold;
                    title.enableAutoSizing = false;
                    title.fontSize = 76f;
                    title.margin = Vector4.zero;
                    title.raycastTarget = false;
                }
            }

            if (header.Find("CloseButton") is RectTransform closeRect)
            {
                closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0.5f);
                closeRect.pivot = new Vector2(0.5f, 0.5f);
                closeRect.anchoredPosition = new Vector2(-45f, 0f);
                closeRect.sizeDelta = new Vector2(76f, 76f);

                if (closeRect.Find("X") is RectTransform xRect)
                {
                    xRect.anchorMin = Vector2.zero;
                    xRect.anchorMax = Vector2.one;
                    xRect.pivot = new Vector2(0.5f, 0.5f);
                    xRect.offsetMin = new Vector2(2f, 2f);
                    xRect.offsetMax = new Vector2(-2f, -2f);

                    var xText = xRect.GetComponent<TextMeshProUGUI>();
                    if (xText != null)
                    {
                        xText.text = "X";
                        xText.color = Color.white;
                        xText.alignment = TextAlignmentOptions.Center;
                        xText.fontStyle = FontStyles.Bold;
                        xText.enableAutoSizing = false;
                        xText.fontSize = 60f;
                        xText.margin = Vector4.zero;
                        xText.textWrappingMode = TextWrappingModes.NoWrap;
                        xText.overflowMode = TextOverflowModes.Overflow;
                        xText.raycastTarget = false;
                    }
                }
            }
        }

        Transform tabs = window.Find("CategoryTabs");
        if (tabs is RectTransform tabsRect)
        {
            tabsRect.anchorMin = new Vector2(0f, 0f);
            tabsRect.anchorMax = new Vector2(0f, 1f);
            tabsRect.pivot = new Vector2(0f, 1f);
            tabsRect.offsetMin = new Vector2(18f, 24f);
            tabsRect.offsetMax = new Vector2(188f, -122f);

            var horizontalLayout = tabs.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout != null)
                horizontalLayout.enabled = false;

            var verticalLayout = tabs.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout == null)
                verticalLayout = tabs.gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.enabled = true;
            verticalLayout.padding = new RectOffset(10, 10, 10, 10);
            verticalLayout.spacing = 14f;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            var tabsImage = tabs.GetComponent<Image>();
            if (tabsImage == null)
                tabsImage = tabs.gameObject.AddComponent<Image>();
            var windowImage = window.GetComponent<Image>();
            tabsImage.sprite = windowImage != null ? windowImage.sprite : null;
            tabsImage.type = tabsImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            tabsImage.color = new Color(0.16f, 0.07f, 0.025f, 0.96f);
            tabsImage.raycastTarget = false;
        }

        for (int i = 0; i < _categoryButtons.Count; i++)
            ConfigureCategoryButton(_categoryButtons[i]);

        if (_scrollRect != null && _scrollRect.transform is RectTransform scrollRect)
        {
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(206f, 24f);
            scrollRect.offsetMax = new Vector2(-18f, -122f);
            _scrollRect.horizontal = false;
        }

        if (_content != null)
        {
            var contentLayout = _content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.padding = new RectOffset(10, 10, 10, 10);
                contentLayout.spacing = 16f;
                contentLayout.childAlignment = TextAnchor.UpperCenter;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = true;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;
            }
        }
    }

    private void UpdateResponsiveWindowScale(bool force = false)
    {
        if (_shopCanvas == null)
            return;

        var windowRect = _shopCanvas.transform.Find("Window") as RectTransform;
        if (windowRect != null)
            UpdateResponsiveWindowScale(windowRect, force);
    }

    private void UpdateResponsiveWindowScale(RectTransform windowRect, bool force)
    {
        var viewportRect = _shopCanvas.transform as RectTransform;
        var canvas = _shopCanvas.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        Vector2 viewportSize = viewportRect != null ? viewportRect.rect.size : Vector2.zero;
        if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            viewportSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            return;
        if (!force && (viewportSize - _lastShopViewportSize).sqrMagnitude < 0.25f)
            return;

        float availableWidth = Mathf.Max(1f, viewportSize.x - ShopSafeMargin * 2f);
        float availableHeight = Mathf.Max(1f, viewportSize.y - ShopSafeMargin * 2f);
        float scale = Mathf.Clamp(
            Mathf.Min(availableWidth / ShopWindowSize.x, availableHeight / ShopWindowSize.y),
            0.1f,
            1f);

        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = ShopWindowSize;
        windowRect.localScale = new Vector3(scale, scale, 1f);
        _lastShopViewportSize = viewportSize;
    }

    private static void ConfigureCategoryButton(Button button)
    {
        if (button == null)
            return;

        var layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 150f;
        layout.preferredHeight = 150f;
        layout.flexibleHeight = 0f;
        layout.minWidth = 150f;
        layout.preferredWidth = 150f;
        layout.flexibleWidth = 0f;

        var background = button.targetGraphic as Image;
        if (background != null && background.sprite != null)
            background.type = Image.Type.Sliced;

        Transform iconTransform = button.transform.Find("NavigationIcon");
        if (iconTransform is RectTransform iconRect)
        {
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 16f);
            iconRect.sizeDelta = new Vector2(108f, 108f);
            var icon = iconTransform.GetComponent<Image>();
            if (icon != null)
            {
                icon.preserveAspect = true;
                var legacyOutline = icon.GetComponent<UnityEngine.UI.Outline>();
                if (legacyOutline != null)
                    legacyOutline.enabled = false;
            }
        }

        Transform labelTransform = button.transform.Find("Label");
        if (labelTransform is RectTransform labelRect)
        {
            labelTransform.gameObject.SetActive(true);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 3f);
            labelRect.sizeDelta = new Vector2(-8f, 30f);

            var label = labelTransform.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = Color.white;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 26f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.margin = new Vector4(2f, 0f, 2f, 0f);
                label.raycastTarget = false;

                var labelOutline = label.GetComponent<UnityEngine.UI.Outline>();
                if (labelOutline == null)
                    labelOutline = label.gameObject.AddComponent<UnityEngine.UI.Outline>();
                labelOutline.effectColor = new Color(0f, 0f, 0f, 1f);
                labelOutline.effectDistance = new Vector2(2f, -2f);
                labelOutline.useGraphicAlpha = true;
            }
        }
    }

    private static string GetPurchaseId(ShopPackData pack)
    {
        // Mirra product IDs must match the configured catalog exactly.
        // The old synthetic "Pack_" prefix made every real-money SKU differ
        // from the IDs supplied to the platform (for example,
        // limited_egg_10 became Pack_limited_egg_10).
        return pack != null ? pack.Id : string.Empty;
    }

    public static void DeliverOrQueueRestoredPurchase(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        if (G.SpecialShop != null)
        {
            G.SpecialShop.OnPurchaseRestore(id);
            return;
        }

        var state = ReadPendingRestoreState();
        if (!state.Ids.Contains(id))
            state.Ids.Add(id);
        WritePendingRestoreState(state);
    }

    private void LoadPendingRestores()
    {
        var state = ReadPendingRestoreState();
        for (int i = 0; i < state.Ids.Count; i++)
        {
            string id = state.Ids[i];
            if (!string.IsNullOrWhiteSpace(id) && !_pendingRestoredPurchaseIds.Contains(id))
                _pendingRestoredPurchaseIds.Add(id);
        }
    }

    private void QueuePendingRestore(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _pendingRestoredPurchaseIds.Contains(id))
            return;

        _pendingRestoredPurchaseIds.Add(id);
        SavePendingRestores();
    }

    private void RemovePendingRestore(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_pendingRestoredPurchaseIds.Remove(id))
            return;

        SavePendingRestores();
    }

    private void SavePendingRestores()
    {
        var state = new PendingRestoreState();
        state.Ids.AddRange(_pendingRestoredPurchaseIds);
        WritePendingRestoreState(state);
    }

    private static PendingRestoreState ReadPendingRestoreState()
    {
        string json = PlayerPrefs.GetString(PendingRestoresPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return new PendingRestoreState();

        try
        {
            var state = JsonUtility.FromJson<PendingRestoreState>(json);
            if (state == null)
                return new PendingRestoreState();
            state.Ids ??= new List<string>();
            return state;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SpecialShop] Invalid pending-purchase data was reset: {exception.Message}");
            PlayerPrefs.DeleteKey(PendingRestoresPrefsKey);
            PlayerPrefs.Save();
            return new PendingRestoreState();
        }
    }

    private static void WritePendingRestoreState(PendingRestoreState state)
    {
        if (state == null || state.Ids == null || state.Ids.Count == 0)
            PlayerPrefs.DeleteKey(PendingRestoresPrefsKey);
        else
            PlayerPrefs.SetString(PendingRestoresPrefsKey, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }

    private void FlushPendingRestores()
    {
        bool changed = false;
        for (int i = _pendingRestoredPurchaseIds.Count - 1; i >= 0; i--)
        {
            if (GiveReward(_pendingRestoredPurchaseIds[i]))
            {
                _pendingRestoredPurchaseIds.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            SavePendingRestores();
    }
}

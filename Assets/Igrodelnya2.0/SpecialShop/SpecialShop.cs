using MirraGames.SDK.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShop : MonoBehaviour
{
    public event Action Closed;

    [SerializeField] private List<ShopPackData> _packs;
    [SerializeField] private LocalizationData _localizationData;
    [SerializeField] private GameObject _shopCanvas;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Transform _content;
    [SerializeField] private SpecialShopSlot _slotPrefab;
    [SerializeField] private ShopRow _rowPrefab;
    [SerializeField] private SpecialShopSectionHeader _sectionHeaderPrefab;
    [SerializeField] private int _maxItemsPerRow = 2;
    [SerializeField] private ShopCategory _defaultCategory = ShopCategory.Featured;
    [SerializeField] private List<Button> _categoryButtons = new();
    [SerializeField] private Color _selectedTabColor = new Color(0.2f, 0.84f, 0.08f, 1f);
    [SerializeField] private Color _normalTabColor = new Color(0.31f, 0.16f, 0.07f, 1f);
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
    private readonly List<string> _pendingRestoredPurchaseIds = new();
    private readonly List<ShopRow> _rows = new();
    private readonly List<SpecialShopSlot> _slots = new();
    private readonly List<SpecialShopSectionHeader> _sectionHeaders = new();
    private readonly Dictionary<ShopCategory, RectTransform> _sectionAnchors = new();
    private ShopEffectsService _effects;
    private ShopCategory _currentCategory;
    private bool _isOpen;
    private bool _slotsInitialized;
    private bool _lastPurchasesAvailable;
    private bool _rewardedAdPurchasePending;
    private Coroutine _scrollCoroutine;
    private float _nextPlatformRefresh;
    private LocalizationManager _subscribedLocalizationManager;

    private static readonly ShopCategory[] SectionCategories =
    {
        ShopCategory.Featured,
        ShopCategory.Boosts,
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
        LocalizationUtils.ConfigureFallback(_localizationData);
        _effects = ShopEffectsService.EnsureExists();
        _currentCategory = _defaultCategory;
        ApplyLayout();
    }

    private void Start()
    {
        _effects?.RestoreOwnedPermanentEffects(_packs);
        InitSlots();
        G.Purchases?.RestorePurchases();
    }

    private void Update()
    {
        if (!_isOpen || Time.unscaledTime < _nextPlatformRefresh)
            return;

        _nextPlatformRefresh = Time.unscaledTime + 1f;
        bool purchasesAvailable = PurchasesAvailable();
        if (purchasesAvailable != _lastPurchasesAvailable)
            InitSlots();
    }

    private void OnEnable()
    {
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        SubscribeToLocalizationManager(LocalizationManager.Instance);

        if (_effects != null)
            _effects.Changed += RefreshSlots;
        if (G.Currency != null)
        {
            G.Currency.NoGems.RemoveListener(OpenCurrency);
            G.Currency.NoGems.AddListener(OpenCurrency);
        }
    }

    private void OnDisable()
    {
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        UnsubscribeFromLocalizationManager();

        if (_effects != null)
            _effects.Changed -= RefreshSlots;
        if (G.Currency != null)
            G.Currency.NoGems.RemoveListener(OpenCurrency);
    }

    private void OnDestroy()
    {
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        UnsubscribeFromLocalizationManager();

        if (G.SpecialShop == this)
            G.SpecialShop = null;
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

    public void InitSlots()
    {
        ClearSlots();
        _slotsInitialized = false;
        _purchaseData.Clear();

        if (_content == null || _slotPrefab == null || _rowPrefab == null || _sectionHeaderPrefab == null)
        {
            Debug.LogError("[SpecialShop] Content, slot, row or section header prefab is missing.");
            return;
        }

        _lastPurchasesAvailable = PurchasesAvailable();
        for (int categoryIndex = 0; categoryIndex < SectionCategories.Length; categoryIndex++)
        {
            ShopCategory category = SectionCategories[categoryIndex];
            var sectionPacks = GetSectionPacks(category);
            if (sectionPacks.Count == 0)
                continue;

            var header = Instantiate(_sectionHeaderPrefab, _content);
            GetSectionTitle(category, out string key, out string fallback);
            header.Init(key, fallback);
            _sectionHeaders.Add(header);
            _sectionAnchors[category] = header.RectTransform;

            var sectionRows = new List<ShopRow>();
            for (int i = 0; i < sectionPacks.Count; i++)
            {
                var item = sectionPacks[i];
                Transform row = GetOrCreateAvailableRow(item.SlotType == ShopSlotType.Big, sectionRows);
                var slot = Instantiate(_slotPrefab, row);
                string purchaseId = GetPurchaseId(item);
                PurchaseData data = ResolvePurchaseData(item, purchaseId);

                if (!_purchaseData.ContainsKey(purchaseId))
                    _purchaseData.Add(purchaseId, item);

                slot.Init(this, item, data);
                _slots.Add(slot);
            }
        }

        _slotsInitialized = true;
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
        _isOpen = true;
        _shopCanvas.SetActive(true);
        bool purchasesAvailable = PurchasesAvailable();
        if (!_slotsInitialized || purchasesAvailable != _lastPurchasesAvailable)
            InitSlots();
        if (G.Control != null)
            G.Control.CursorActive = true;
        G.Currency?.ShowGems?.Invoke(true);
        G.Input?.AOpenWindow?.Invoke(this);
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
        _shopCanvas.SetActive(false);
        if (G.Control != null)
            G.Control.CursorActive = false;
        G.Sound?.Play(GameAudioId.SFX_UI_CLOSE);
        if (wasOpen)
            Closed?.Invoke();
    }

    public void OnPurchaseRestore(string id)
    {
        if (!_slotsInitialized)
        {
            if (!string.IsNullOrWhiteSpace(id) && !_pendingRestoredPurchaseIds.Contains(id))
                _pendingRestoredPurchaseIds.Add(id);
            return;
        }

        GiveReward(id);
        GameAnalytics.TrackCritical(AnalyticsEventNames.PurchaseResult, GameAnalytics.Params(
            "product_id", id,
            "pack_id", id,
            "is_restore", true,
            "source", "purchase_restore",
            "result", "success"),
            "restore:" + id);
    }

    public void TryBuy(PurchaseData purchaseData, ShopPackData packData)
    {
        if (packData == null)
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

        if (IsRewardedAdFallback(packData))
        {
            TryRewardedAdPurchase(packData);
            return;
        }

        if (packData.PriceCurrencyType != CurrencyType.Real)
        {
            if (G.Currency != null && G.Currency.RemoveCurrency(packData.PriceCurrencyType, packData.Price))
                GiveRewards(packData);
            return;
        }

        if (G.Purchases == null || purchaseData == null || string.IsNullOrWhiteSpace(purchaseData.Id))
        {
            Debug.LogWarning("[SpecialShop] Purchases are not ready.");
            return;
        }

        G.IsPaused = true;
        G.Purchases.BuyPurchase(
            purchaseData.Id,
            success =>
            {
                if (success)
                    GiveReward(purchaseData.Id);

                G.IsPaused = false;
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
            && !PurchasesAvailable();
    }

    public string BuildRewardSummary(ShopPackData pack)
    {
        if (pack == null || pack.Rewards == null || pack.Rewards.Count == 0)
            return string.Empty;

        var reward = pack.Rewards[0];
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
            case ShopRewardType.ConsumableIncomeBoost:
                return LocalizationUtils.Format("UI/Shop/TimedIncome", "x{0} income for {1} min", reward.EffectValue, reward.DurationMinutes);
            case ShopRewardType.ConsumableElementLuckBoost:
                return LocalizationUtils.Format("UI/Shop/TimedLuck", "+{0}% elemental egg chance for {1} min", reward.EffectValue, reward.DurationMinutes);
            case ShopRewardType.ConsumableHatchSkip:
                return LocalizationUtils.Format("UI/Shop/HatchSkip", "-{0} min for all active eggs", reward.DurationMinutes);
            case ShopRewardType.Item:
                return LocalizationUtils.Format("UI/Shop/ItemAmount", "Item x{0}", Mathf.Max(1, reward.Amount));
            default:
                return string.Empty;
        }
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

    private Transform GetOrCreateAvailableRow(bool big, List<ShopRow> sectionRows)
    {
        if (!big)
        {
            for (int i = 0; i < sectionRows.Count; i++)
            {
                if (sectionRows[i] != null && sectionRows[i].ItemsCount < sectionRows[i].MaxItems)
                    return sectionRows[i].transform;
            }
        }

        var newRow = Instantiate(_rowPrefab, _content);
        newRow.setMaxItems(big ? 1 : Mathf.Max(1, _maxItemsPerRow));
        sectionRows.Add(newRow);
        _rows.Add(newRow);
        return newRow.transform;
    }

    private PurchaseData ResolvePurchaseData(ShopPackData pack, string purchaseId)
    {
        if (pack == null || pack.PriceCurrencyType != CurrencyType.Real || !_lastPurchasesAvailable)
            return PurchaseData.Fallback(purchaseId);

        if (G.Purchases != null && G.Purchases.PurchasesAvailable())
            return G.Purchases.GetPurchaseData(purchaseId);

        if (IsInAppPreviewActive())
            return new PurchaseData(purchaseId, string.Empty, string.Empty, _inAppPreviewPrice, string.Empty);

        return PurchaseData.Fallback(purchaseId);
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
                fallback = "Усиления";
                break;
            case ShopCategory.Permanent:
                key = "UI/Shop/SectionPermanent";
                fallback = "Навсегда";
                break;
            case ShopCategory.Currency:
                key = "UI/Shop/SectionCurrency";
                fallback = "Кристаллы";
                break;
            default:
                key = "UI/Shop/SectionFeatured";
                fallback = "Лучшее";
                break;
        }
    }

    private void GiveReward(string purchaseId)
    {
        if (string.IsNullOrWhiteSpace(purchaseId) || !_purchaseData.TryGetValue(purchaseId, out var packData))
        {
            string packId = purchaseId != null && purchaseId.StartsWith("Pack_", StringComparison.Ordinal)
                ? purchaseId.Substring(5)
                : purchaseId;
            packData = _packs?.Find(x => x != null && string.Equals(x.Id, packId, StringComparison.Ordinal));
        }

        if (packData != null)
            GiveRewards(packData);
    }

    private void GiveRewards(ShopPackData packData)
    {
        if (packData == null || packData.Rewards == null)
            return;

        using (GameAnalytics.BeginItemGrant(
                   "special_shop",
                   packData.Id,
                   packData.PriceCurrencyType.ToString().ToLowerInvariant(),
                   packData.Price))
        {
            for (int rewardIndex = 0; rewardIndex < packData.Rewards.Count; rewardIndex++)
            {
                var reward = packData.Rewards[rewardIndex];
                switch (reward.Type)
                {
                    case ShopRewardType.Item:
                        if (reward.Item == null || G.Inventory == null)
                            break;
                        for (int i = 0; i < Mathf.Max(1, reward.Amount); i++)
                            G.Inventory.Add(Instantiate(reward.Item));
                        break;
                    case ShopRewardType.Currency:
                        G.Currency?.AddCurrency(reward.RewardCurrencyType, reward.Amount);
                        break;
                    case ShopRewardType.NoAdsMonth:
                        G.Ad?.DisableInterstitialAdsForDays(reward.Amount > 0 ? reward.Amount : 30);
                        break;
                    case ShopRewardType.NoAdsForever:
                        G.Ad?.DisableInterstitialAdsForever();
                        break;
                    default:
                        _effects?.GrantReward(packData.Id, reward);
                        break;
                }
            }
        }

        var majorPurchase =
            packData.PriceCurrencyType == CurrencyType.Real ||
            packData.Category == ShopCategory.Permanent;
        G.Sound?.Play(majorPurchase ? GameAudioId.SFX_SHOP_PURCHASE_MAJOR : GameAudioId.SFX_SHOP_PURCHASE);
        RefreshSlots();
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
    }

    private void UpdateCategoryButtons()
    {
        for (int i = 0; i < _categoryButtons.Count; i++)
        {
            var button = _categoryButtons[i];
            if (button == null)
                continue;

            ShopCategory category = i < ButtonCategories.Length ? ButtonCategories[i] : (ShopCategory)i;
            bool available = category != ShopCategory.Permanent && GetSectionPacks(category).Count > 0;
            button.gameObject.SetActive(available);
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

        Transform window = _shopCanvas.transform.Find("Window");
        if (window == null)
            return;

        if (window is RectTransform windowRect)
        {
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.sizeDelta = new Vector2(1120f, 760f);
        }

        Transform header = window.Find("Header");
        if (header is RectTransform headerRect)
        {
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 86f);
        }

        Transform tabs = window.Find("CategoryTabs");
        if (tabs is RectTransform tabsRect)
        {
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = Vector2.one;
            tabsRect.pivot = new Vector2(0.5f, 1f);
            tabsRect.anchoredPosition = new Vector2(0f, -96f);
            tabsRect.sizeDelta = new Vector2(-48f, 68f);

            var verticalLayout = tabs.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout != null)
                verticalLayout.enabled = false;

            var horizontalLayout = tabs.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout == null)
                horizontalLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = new RectOffset(0, 0, 0, 0);
            horizontalLayout.spacing = 12f;
            horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = true;
            horizontalLayout.childForceExpandHeight = true;
        }

        for (int i = 0; i < _categoryButtons.Count; i++)
            ConfigureCategoryButton(_categoryButtons[i]);

        if (_scrollRect != null && _scrollRect.transform is RectTransform scrollRect)
        {
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(24f, 24f);
            scrollRect.offsetMax = new Vector2(-24f, -176f);
            _scrollRect.horizontal = false;
        }

        if (_content != null)
        {
            var contentLayout = _content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.padding = new RectOffset(8, 8, 8, 8);
                contentLayout.spacing = 14f;
                contentLayout.childAlignment = TextAnchor.UpperCenter;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = true;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;
            }
        }
    }

    private static void ConfigureCategoryButton(Button button)
    {
        if (button == null)
            return;

        var layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 68f;
        layout.preferredHeight = 68f;
        layout.flexibleWidth = 1f;

        Transform iconTransform = button.transform.Find("NavigationIcon");
        if (iconTransform is RectTransform iconRect)
        {
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(34f, 0f);
            iconRect.sizeDelta = new Vector2(42f, 42f);
            var icon = iconTransform.GetComponent<Image>();
            if (icon != null)
                icon.preserveAspect = true;
        }

        Transform labelTransform = button.transform.Find("Label");
        if (labelTransform is RectTransform labelRect)
        {
            labelTransform.gameObject.SetActive(true);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelTransform.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 15f;
                label.fontSizeMax = 24f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.margin = new Vector4(iconTransform != null ? 58f : 8f, 5f, 8f, 5f);
                label.raycastTarget = false;
            }
        }
    }

    private static string GetPurchaseId(ShopPackData pack)
    {
        return "Pack_" + (pack != null ? pack.Id : string.Empty);
    }

    private void FlushPendingRestores()
    {
        for (int i = 0; i < _pendingRestoredPurchaseIds.Count; i++)
            GiveReward(_pendingRestoredPurchaseIds[i]);
        _pendingRestoredPurchaseIds.Clear();
    }
}

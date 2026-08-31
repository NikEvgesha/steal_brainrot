using MirraGames.SDK.Common;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShopSlot : MonoBehaviour
{
    [Header("Template Layout")]
    [SerializeField, Tooltip("Keep the RectTransform layout authored in this prefab instead of rebuilding it at runtime.")]
    private bool _useAuthoredLayout;
    [SerializeField] private ShopOfferStyle _authoredLayoutStyle;

    [Header("Content")]
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private Image _productIcon;
    [SerializeField] private Image _cardBackground;
    [SerializeField] private Transform _rewardParent;
    [SerializeField] private AdaptiveGridSpawner _rewardsGrid;
    [SerializeField] private SpecialShopRewardSlot _rewardPrefab;

    [Header("Purchase")]
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _price;
    [SerializeField, Tooltip("Optional alternative label for a real-money/platform price. Keep the PriceVal object disabled in the prefab.")]
    private TextMeshProUGUI _platformPrice;
    [SerializeField] protected Image _currencyIcon;
    [SerializeField] protected TextMeshProUGUI _currencyText;

    [Header("Consumable")]
    [SerializeField] private Button _useButton;
    [SerializeField] private TextMeshProUGUI _useButtonText;
    [SerializeField] private TextMeshProUGUI _ownedText;
    [SerializeField] private TextMeshProUGUI _activeTimerText;

    private SpecialShop _shop;
    private ShopPackData _shopPackData;
    private PurchaseData _productData;
    private float _nextTimerRefresh;
    [SerializeField, HideInInspector] private Image _badgeBackground;
    [SerializeField, HideInInspector] private TextMeshProUGUI _badgeText;
    [SerializeField, HideInInspector] private TextMeshProUGUI _originalPriceText;
    [SerializeField, HideInInspector] private SpecialShopEternalTrackView _eternalTrack;

    public ShopPackData PackData => _shopPackData;

    private void Awake()
    {
        ResolveOptionalPlatformPrice();
        if (_platformPrice != null)
            _platformPrice.gameObject.SetActive(false);

        if (!_useAuthoredLayout)
        {
            ApplyCardLayout(_shopPackData != null
                ? _shopPackData.OfferStyle
                : ShopOfferStyle.Standard);
        }
    }

    public void Init(SpecialShop shop, ShopPackData pack, PurchaseData purchaseData)
    {
        _shop = shop;
        _shopPackData = pack;
        _productData = purchaseData;

        if (pack == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!_useAuthoredLayout)
            ApplyCardLayout(pack.OfferStyle);
        ConfigureOfferDecorations(pack);

        if (_name != null)
            _name.text = _shop != null
                ? _shop.BuildPackTitle(pack)
                : LocalizationUtils.T(pack.Name, pack.Name);
        if (_description != null)
        {
            _description.text = _shop != null
                ? _shop.BuildPackDescription(pack)
                : LocalizationUtils.T(pack.DescriptionKey, pack.DescriptionFallback);
            _description.gameObject.SetActive(ShouldShowDescription(pack));
        }

        bool eternalPack = pack.OfferStyle == ShopOfferStyle.EternalPack;
        bool hasMultipleRewards = pack.Rewards != null && pack.Rewards.Count > 1;
        if (_effectText != null)
        {
            _effectText.gameObject.SetActive(!eternalPack && !hasMultipleRewards);
            _effectText.text = _shop != null ? _shop.BuildRewardSummary(pack) : string.Empty;
        }

        var icon = ResolveProductIcon(pack);
        if (_productIcon != null)
        {
            _productIcon.sprite = icon;
            _productIcon.enabled = !eternalPack && icon != null;
            _productIcon.gameObject.SetActive(!eternalPack);
            _productIcon.preserveAspect = true;
        }

        if (_cardBackground != null)
            _cardBackground.color = Color.Lerp(pack.AccentColor, Color.white, 0.18f);

        if (_rewardParent != null)
            _rewardParent.gameObject.SetActive(!eternalPack);

        if (eternalPack)
            ConfigureEternalTrack(pack);
        else
            BuildRewardIcons(pack);

        // Reward-grid rebuilding can restore prefab RectTransform values on sibling
        // artwork. Apply the offer-specific layout once more after all dynamic
        // content has been created so the final card geometry is deterministic.
        if (!_useAuthoredLayout)
            ApplyCardLayout(pack.OfferStyle);
        ConfigureOfferDecorations(pack);
        ConfigureActionLayout(pack.HasConsumableReward, pack.OfferStyle);
        RefreshPrice();
        RefreshState();
    }

    private void Update()
    {
        if (_shopPackData == null ||
            (_shop != null && !_shop.Opened) ||
            !RequiresPeriodicRefresh(_shopPackData) ||
            Time.unscaledTime < _nextTimerRefresh)
            return;

        bool hasActiveTimer = HasTimerReward(_shopPackData);
        _nextTimerRefresh = Time.unscaledTime + (hasActiveTimer ? 1f : 60f);
        if (hasActiveTimer)
            RefreshActiveTimer(_shop != null ? _shop.Effects : null);

        if (_shopPackData.AvailabilityDays > 0)
            RefreshAvailabilityBadge();
    }

    /// <summary>
    /// Currency changes are frequent while the farm is producing income. They
    /// only affect whether this card can be bought, so avoid rebuilding reward
    /// grids, recalculating farm income or refreshing the eternal track here.
    /// </summary>
    public void RefreshAffordability(CurrencyType changedCurrency)
    {
        if (_shopPackData == null ||
            _buyButton == null ||
            _shopPackData.PriceCurrencyType != changedCurrency)
            return;

        var effects = _shop != null ? _shop.Effects : null;
        bool permanentOwned = effects != null && effects.IsPermanentPackOwned(_shopPackData);
        bool interactable = !permanentOwned && (_shop == null || _shop.CanPurchasePack(_shopPackData));
        if (_buyButton.interactable != interactable)
            _buyButton.interactable = interactable;
    }

    public void RefreshState()
    {
        if (_shopPackData == null)
            return;

        if (_shopPackData.OfferStyle == ShopOfferStyle.EternalPack)
        {
            _eternalTrack?.Refresh();
            return;
        }

        var effects = _shop != null ? _shop.Effects : null;
        bool permanentOwned = effects != null && effects.IsPermanentPackOwned(_shopPackData);
        int owned = effects != null ? effects.GetOwnedCount(_shopPackData) : 0;

        if (_buyButton != null)
            _buyButton.interactable = !permanentOwned && (_shop == null || _shop.CanPurchasePack(_shopPackData));
        if (permanentOwned)
            SetPriceText(LocalizationUtils.T("UI/Shop/Owned", "Куплено"), false);
        else
            RefreshPrice();

        if (_useButton != null)
        {
            _useButton.gameObject.SetActive(_shopPackData.HasConsumableReward);
            _useButton.interactable = owned > 0;
        }

        if (_useButtonText != null)
            _useButtonText.text = LocalizationUtils.T("UI/Shop/Use", "Применить");
        if (_ownedText != null)
        {
            _ownedText.gameObject.SetActive(_shopPackData.HasConsumableReward);
            _ownedText.text = LocalizationUtils.Format("UI/Shop/OwnedCount", "В наличии: {0}", owned);
        }

        RefreshActiveTimer(effects);
        RefreshAvailabilityBadge();
    }

    public void RefreshPurchaseData(PurchaseData purchaseData)
    {
        _productData = purchaseData ?? PurchaseData.Fallback(_shopPackData != null ? _shopPackData.Id : string.Empty);
        RefreshPrice();
        RefreshState();
    }

    public void OnClick()
    {
        _shop?.TryBuy(_productData, _shopPackData);
    }

    public void OnUseClick()
    {
        _shop?.TryUse(_shopPackData);
    }

    private void RefreshPrice()
    {
        if (_shopPackData == null)
            return;

        bool rewardedAdFallback = _shop != null && _shop.IsRewardedAdFallback(_shopPackData);
        bool realPurchase = _shopPackData.PriceCurrencyType == CurrencyType.Real;
        string providerPrice = _productData != null ? _productData.DisplayPrice : string.Empty;
        bool platformPriceReady = !realPurchase || !string.IsNullOrWhiteSpace(providerPrice);
        string displayedPrice = rewardedAdFallback
            ? LocalizationUtils.Format("UI/Shop/RewardedAdPrice", "+{0}", _shopPackData.RewardedAdGems)
            : realPurchase
                ? platformPriceReady ? providerPrice : "…"
                : _shopPackData.Price.ToString();
        SetPriceText(displayedPrice, realPurchase && !rewardedAdFallback);

        if (_currencyIcon != null)
        {
            Sprite icon = rewardedAdFallback
                ? _shop.RewardedAdIcon
                : realPurchase
                    ? null
                    : G.Currency != null ? G.Currency.GetCurrencyIcon(_shopPackData.PriceCurrencyType) : null;
            _currencyIcon.sprite = icon;
            _currencyIcon.enabled = icon != null;
            _currencyIcon.gameObject.SetActive(icon != null);
        }

        if (_currencyText != null)
            _currencyText.text = rewardedAdFallback || realPurchase
                ? string.Empty
                : LocalizationUtils.T(
                    "UI/Currency/" + _shopPackData.PriceCurrencyType,
                    _shopPackData.PriceCurrencyType.ToString());

        if (_buyButton != null && realPurchase && !rewardedAdFallback)
            _buyButton.interactable = platformPriceReady &&
                                      (_shop == null || _shop.CanPurchasePack(_shopPackData));
    }

    private void SetPriceText(string value, bool usePlatformLayout)
    {
        ResolveOptionalPlatformPrice();
        bool showPlatformPrice = usePlatformLayout && _platformPrice != null;

        if (_platformPrice != null)
        {
            if (showPlatformPrice)
                _platformPrice.text = value;
            if (_platformPrice.gameObject.activeSelf != showPlatformPrice)
                _platformPrice.gameObject.SetActive(showPlatformPrice);
        }

        if (_price != null)
        {
            if (!showPlatformPrice)
                _price.text = value;
            if (_price.gameObject.activeSelf == showPlatformPrice)
                _price.gameObject.SetActive(!showPlatformPrice);
        }
    }

    private void ResolveOptionalPlatformPrice()
    {
        if (_platformPrice != null)
            return;

        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (string.Equals(texts[i].gameObject.name, "PriceVal", StringComparison.Ordinal))
            {
                _platformPrice = texts[i];
                return;
            }
        }
    }

    private void RefreshActiveTimer(ShopEffectsService effects)
    {
        if (_activeTimerText == null)
            return;

        int seconds = 0;
        if (effects != null && _shopPackData.Rewards != null)
        {
            for (int i = 0; i < _shopPackData.Rewards.Count; i++)
            {
                var type = _shopPackData.Rewards[i].Type;
                if (type == ShopRewardType.ConsumableIncomeBoost ||
                    type == ShopRewardType.TimedIncomeBoost ||
                    type == ShopRewardType.ConsumableElementLuckBoost ||
                    type == ShopRewardType.TimedElementLuckBoost ||
                    type == ShopRewardType.ConsumableHatchSpeedBoost ||
                    type == ShopRewardType.ConsumableOmniBoost)
                {
                    seconds = effects.GetRemainingSeconds(type);
                    break;
                }
            }
        }

        bool shouldShow = seconds > 0;
        if (_activeTimerText.gameObject.activeSelf != shouldShow)
            _activeTimerText.gameObject.SetActive(shouldShow);
        if (seconds > 0)
        {
            TimeSpan remaining = TimeSpan.FromSeconds(seconds);
            string timer = seconds >= 3600
                ? remaining.ToString(@"hh\:mm\:ss")
                : remaining.ToString(@"mm\:ss");
            string label = LocalizationUtils.Format("UI/Shop/ActiveTimer", "Активно: {0}", timer);
            if (!string.Equals(_activeTimerText.text, label, StringComparison.Ordinal))
                _activeTimerText.text = label;
        }
    }

    private void BuildRewardIcons(ShopPackData pack)
    {
        if (_rewardParent == null || _rewardPrefab == null)
            return;

        if (_rewardsGrid == null)
            _rewardsGrid = _rewardParent.GetComponent<AdaptiveGridSpawner>();

        _rewardsGrid?.ClearSpawnedItems();

        for (int i = _rewardParent.childCount - 1; i >= 0; i--)
        {
            var child = _rewardParent.GetChild(i);
            if (child != _rewardPrefab.transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        _rewardPrefab.gameObject.SetActive(false);
        if (pack.Rewards == null || pack.Rewards.Count <= 1)
        {
            _rewardsGrid?.Rebuild();
            return;
        }

        for (int i = 0; i < pack.Rewards.Count; i++)
        {
            var reward = pack.Rewards[i];
            var rewardView = _rewardsGrid != null
                ? _rewardsGrid.SpawnObject<SpecialShopRewardSlot>(_rewardPrefab.gameObject)
                : Instantiate(_rewardPrefab, _rewardParent);
            if (rewardView == null)
                continue;

            rewardView.gameObject.SetActive(true);
            rewardView.SetReward(ResolveRewardIcon(reward), Mathf.Max(1, reward.Amount));
        }

        _rewardsGrid?.Rebuild();
    }

    private static Sprite ResolveProductIcon(ShopPackData pack)
    {
        if (pack.Icon != null)
            return pack.Icon;
        if (pack.Rewards == null || pack.Rewards.Count == 0)
            return null;
        return ResolveRewardIcon(pack.Rewards[0]);
    }

    private static Sprite ResolveRewardIcon(ShopReward reward)
    {
        if (reward.Icon != null)
            return reward.Icon;
        if (reward.Type == ShopRewardType.Item && reward.Item != null)
            return reward.Item.Icon;
        return null;
    }

    [ContextMenu("Apply shop card layout")]
    public void ApplyCardLayout()
    {
        ApplyCardLayout(_useAuthoredLayout
            ? _authoredLayoutStyle
            : _shopPackData != null
                ? _shopPackData.OfferStyle
                : ShopOfferStyle.Standard);
    }

#if UNITY_EDITOR
    public void ConfigureAsAuthoredTemplate(ShopOfferStyle style)
    {
        _useAuthoredLayout = false;
        _authoredLayoutStyle = style;
        ApplyCardLayout(style);
        EnsureDecorations();

        bool monthly = style == ShopOfferStyle.MonthlyPass;
        if (_badgeBackground != null && _badgeBackground.transform is RectTransform badgeRect)
        {
            badgeRect.anchorMin = monthly ? new Vector2(0.56f, 0.73f) : new Vector2(0.70f, 0.85f);
            badgeRect.anchorMax = monthly ? new Vector2(0.96f, 0.81f) : new Vector2(0.98f, 0.98f);
            badgeRect.offsetMin = new Vector2(4f, 1f);
            badgeRect.offsetMax = new Vector2(-4f, -2f);
        }

        if (_originalPriceText != null && monthly)
        {
            ConfigureRect(
                _originalPriceText.rectTransform,
                new Vector2(0.62f, 0.20f),
                new Vector2(0.96f, 0.28f),
                new Vector2(3f, 1f),
                new Vector2(-3f, -1f));
        }

        if (style == ShopOfferStyle.EternalPack && _eternalTrack == null)
        {
            var trackObject = new GameObject("EternalTrack", typeof(RectTransform));
            trackObject.transform.SetParent(transform, false);
            _eternalTrack = trackObject.AddComponent<SpecialShopEternalTrackView>();
        }

        _badgeBackground.gameObject.SetActive(false);
        if (_originalPriceText != null)
            _originalPriceText.gameObject.SetActive(false);
        if (_eternalTrack != null)
            _eternalTrack.gameObject.SetActive(style == ShopOfferStyle.EternalPack);
        _useAuthoredLayout = true;
    }
#endif

    private void ApplyCardLayout(ShopOfferStyle style)
    {
        var layout = GetComponent<LayoutElement>();
        if (layout == null)
            layout = gameObject.AddComponent<LayoutElement>();
        float height = GetPreferredHeight(style);
        layout.minHeight = height;
        layout.preferredHeight = height;

        bool strip = style == ShopOfferStyle.TimedIncome || style == ShopOfferStyle.TimedLuck;
        bool eternal = style == ShopOfferStyle.EternalPack;
        bool monthlyPass = style == ShopOfferStyle.MonthlyPass;
        if (strip)
        {
            ConfigureRect(_name != null ? _name.rectTransform : null, new Vector2(0.02f, 0.72f), new Vector2(0.72f, 0.98f), new Vector2(12f, 2f), new Vector2(-8f, -2f));
            ConfigureRect(_productIcon != null ? _productIcon.rectTransform : null, new Vector2(0.02f, 0.12f), new Vector2(0.24f, 0.72f), new Vector2(8f, 6f), new Vector2(-8f, -6f));
            ConfigureRect(_description != null ? _description.rectTransform : null, new Vector2(0.25f, 0.47f), new Vector2(0.72f, 0.71f), new Vector2(4f, 2f), new Vector2(-4f, -2f));
            ConfigureRect(_effectText != null ? _effectText.rectTransform : null, new Vector2(0.25f, 0.17f), new Vector2(0.72f, 0.69f), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            ConfigureRect(_rewardParent as RectTransform, new Vector2(0.25f, 0.17f), new Vector2(0.72f, 0.69f), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            ConfigureRect(_ownedText != null ? _ownedText.rectTransform : null, new Vector2(0.73f, 0.60f), new Vector2(0.98f, 0.72f), new Vector2(2f, 0f), new Vector2(-2f, 0f));
            ConfigureRect(_activeTimerText != null ? _activeTimerText.rectTransform : null, new Vector2(0.73f, 0.48f), new Vector2(0.98f, 0.60f), new Vector2(2f, 0f), new Vector2(-2f, 0f));
            ConfigureRect(_useButton != null ? _useButton.transform as RectTransform : null, new Vector2(0.73f, 0.26f), new Vector2(0.98f, 0.47f), Vector2.zero, Vector2.zero);
            ConfigureRect(_buyButton != null ? _buyButton.transform as RectTransform : null, new Vector2(0.73f, 0.04f), new Vector2(0.98f, 0.24f), Vector2.zero, Vector2.zero);
        }
        else if (eternal)
        {
            ConfigureRect(_name != null ? _name.rectTransform : null, new Vector2(0.02f, 0.79f), new Vector2(0.72f, 0.98f), new Vector2(12f, 2f), new Vector2(-8f, -2f));
            ConfigureRect(_description != null ? _description.rectTransform : null, new Vector2(0.02f, 0.72f), new Vector2(0.72f, 0.83f), new Vector2(12f, 0f), new Vector2(-8f, 0f));
        }
        else if (monthlyPass)
        {
            // Monthly packs contain a title, a badge, a two-line description,
            // a main icon, multiple rewards and a price. Give every element a
            // dedicated band so localized strings cannot overlap the artwork.
            ConfigureRect(_name != null ? _name.rectTransform : null, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.98f), new Vector2(8f, 2f), new Vector2(-8f, -2f));
            ConfigureRect(_description != null ? _description.rectTransform : null, new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.71f), new Vector2(6f, 2f), new Vector2(-6f, -2f));
            ConfigureRect(_productIcon != null ? _productIcon.rectTransform : null, new Vector2(0.05f, 0.30f), new Vector2(0.32f, 0.55f), new Vector2(8f, 4f), new Vector2(-8f, -4f));
            ConfigureRect(_effectText != null ? _effectText.rectTransform : null, new Vector2(0.36f, 0.30f), new Vector2(0.96f, 0.55f), new Vector2(6f, 4f), new Vector2(-6f, -4f));
            ConfigureRect(_rewardParent as RectTransform, new Vector2(0.36f, 0.30f), new Vector2(0.96f, 0.55f), new Vector2(6f, 4f), new Vector2(-6f, -4f));
            ConfigureRect(_buyButton != null ? _buyButton.transform as RectTransform : null, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.19f), Vector2.zero, Vector2.zero);
            ConfigureRect(_useButton != null ? _useButton.transform as RectTransform : null, new Vector2(0.04f, 0.03f), new Vector2(0.47f, 0.19f), Vector2.zero, Vector2.zero);
            ConfigureRect(_ownedText != null ? _ownedText.rectTransform : null, new Vector2(0.04f, 0.20f), new Vector2(0.47f, 0.28f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
            ConfigureRect(_activeTimerText != null ? _activeTimerText.rectTransform : null, new Vector2(0.52f, 0.20f), new Vector2(0.96f, 0.28f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
        }
        else
        {
            ConfigureRect(_name != null ? _name.rectTransform : null, new Vector2(0f, 0.80f), Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -6f));
            ConfigureRect(_productIcon != null ? _productIcon.rectTransform : null, new Vector2(0.03f, 0.27f), new Vector2(0.34f, 0.76f), new Vector2(10f, 8f), new Vector2(-10f, -8f));
            ConfigureRect(_description != null ? _description.rectTransform : null, new Vector2(0.36f, 0.56f), new Vector2(0.97f, 0.76f), new Vector2(6f, 2f), new Vector2(-6f, -2f));
            ConfigureRect(_effectText != null ? _effectText.rectTransform : null, new Vector2(0.36f, 0.29f), new Vector2(0.97f, 0.55f), new Vector2(6f, 6f), new Vector2(-6f, -6f));
            ConfigureRect(_rewardParent as RectTransform, new Vector2(0.35f, 0.29f), new Vector2(0.97f, 0.55f), new Vector2(6f, 6f), new Vector2(-6f, -6f));
            ConfigureRect(_buyButton != null ? _buyButton.transform as RectTransform : null, new Vector2(0.36f, 0.04f), new Vector2(0.97f, 0.23f), Vector2.zero, Vector2.zero);
            ConfigureRect(_useButton != null ? _useButton.transform as RectTransform : null, new Vector2(0.03f, 0.04f), new Vector2(0.47f, 0.24f), Vector2.zero, Vector2.zero);
            ConfigureRect(_ownedText != null ? _ownedText.rectTransform : null, new Vector2(0.03f, 0.24f), new Vector2(0.47f, 0.36f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
            ConfigureRect(_activeTimerText != null ? _activeTimerText.rectTransform : null, new Vector2(0.52f, 0.24f), new Vector2(0.97f, 0.36f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
        }

        bool compactTitle =
            style == ShopOfferStyle.LimitedEgg ||
            style == ShopOfferStyle.MonthlyPass ||
            style == ShopOfferStyle.Permanent ||
            style == ShopOfferStyle.Potion ||
            style == ShopOfferStyle.PremiumCurrency ||
            style == ShopOfferStyle.SoftCurrency;
        float nameMin = compactTitle ? 12f : strip ? 22f : 18f;
        float nameMax = compactTitle ? 28f : strip ? 36f : 32f;
        ConfigureText(_name, nameMin, nameMax, strip ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, TextOverflowModes.Ellipsis);
        ConfigureText(_description, 14f, 22f, strip ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, TextOverflowModes.Ellipsis);
        ConfigureText(_effectText, 17f, strip ? 31f : 27f, strip ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center, TextOverflowModes.Ellipsis);
        ConfigureText(_ownedText, 13f, 19f, TextAlignmentOptions.Center, TextOverflowModes.Ellipsis);
        ConfigureText(_activeTimerText, 13f, 19f, TextAlignmentOptions.Center, TextOverflowModes.Ellipsis);
        ConfigureText(_useButtonText, 10f, 24f, TextAlignmentOptions.Center, TextOverflowModes.Ellipsis);
        ConfigureText(_price, 18f, 34f, TextAlignmentOptions.Center, TextOverflowModes.Ellipsis);

        if (_productIcon != null)
            _productIcon.preserveAspect = true;
    }

    private void ConfigureActionLayout(bool hasConsumableReward, ShopOfferStyle style)
    {
        if (_buyButton == null)
            return;

        if (style == ShopOfferStyle.EternalPack)
        {
            _buyButton.gameObject.SetActive(false);
            if (_useButton != null)
                _useButton.gameObject.SetActive(false);
            if (_ownedText != null)
                _ownedText.gameObject.SetActive(false);
            if (_activeTimerText != null)
                _activeTimerText.gameObject.SetActive(false);
            return;
        }

        _buyButton.gameObject.SetActive(true);
        if (_useAuthoredLayout)
            return;

        if (style == ShopOfferStyle.TimedIncome || style == ShopOfferStyle.TimedLuck)
        {
            if (!hasConsumableReward)
                ConfigureRect(
                    _buyButton.transform as RectTransform,
                    new Vector2(0.73f, 0.06f),
                    new Vector2(0.98f, 0.34f),
                    Vector2.zero,
                    Vector2.zero);
            return;
        }

        if (style == ShopOfferStyle.MonthlyPass)
        {
            Vector2 monthlyMin = hasConsumableReward ? new Vector2(0.52f, 0.03f) : new Vector2(0.04f, 0.03f);
            ConfigureRect(_buyButton.transform as RectTransform, monthlyMin, new Vector2(0.96f, 0.19f), Vector2.zero, Vector2.zero);
            return;
        }

        Vector2 min = hasConsumableReward ? new Vector2(0.52f, 0.04f) : new Vector2(0.36f, 0.04f);
        ConfigureRect(_buyButton.transform as RectTransform, min, new Vector2(0.97f, 0.24f), Vector2.zero, Vector2.zero);
    }

    private void ConfigureOfferDecorations(ShopPackData pack)
    {
        EnsureDecorations();
        string localizedBadge = _shop != null
            ? _shop.BuildPackBadge(pack)
            : LocalizationUtils.T(pack.BadgeFallback, pack.BadgeFallback);
        bool showBadge = !string.IsNullOrWhiteSpace(localizedBadge);
        bool monthlyPass = pack.OfferStyle == ShopOfferStyle.MonthlyPass;
        if (!_useAuthoredLayout && _badgeBackground.transform is RectTransform badgeRect)
        {
            badgeRect.anchorMin = monthlyPass
                ? new Vector2(0.56f, 0.73f)
                : new Vector2(0.70f, 0.85f);
            badgeRect.anchorMax = monthlyPass
                ? new Vector2(0.96f, 0.81f)
                : new Vector2(0.98f, 0.98f);
            badgeRect.offsetMin = new Vector2(4f, 1f);
            badgeRect.offsetMax = new Vector2(-4f, -2f);
        }

        bool strip = pack.OfferStyle == ShopOfferStyle.TimedIncome ||
                     pack.OfferStyle == ShopOfferStyle.TimedLuck;
        bool eternal = pack.OfferStyle == ShopOfferStyle.EternalPack;
        if (!_useAuthoredLayout && _name != null && showBadge && !strip && !eternal)
        {
            ConfigureRect(
                _name.rectTransform,
                monthlyPass ? new Vector2(0.04f, 0.84f) : new Vector2(0.01f, 0.78f),
                monthlyPass ? new Vector2(0.96f, 0.98f) : new Vector2(0.67f, 0.99f),
                new Vector2(8f, 2f),
                new Vector2(-6f, -3f));
            _name.alignment = TextAlignmentOptions.Center;
        }

        _badgeBackground.gameObject.SetActive(showBadge);
        if (showBadge)
            _badgeText.text = localizedBadge;

        if (_originalPriceText != null)
            _originalPriceText.gameObject.SetActive(false);
    }

    private void RefreshAvailabilityBadge()
    {
        if (_badgeText == null || _shop == null || _shopPackData == null)
            return;

        long seconds = _shop.GetOfferRemainingSeconds(_shopPackData);
        if (seconds < 0L)
            return;

        if (seconds == 0L)
        {
            string expired = LocalizationUtils.T("UI/Shop/Expired", "АКЦИЯ ЗАВЕРШЕНА");
            if (!string.Equals(_badgeText.text, expired, StringComparison.Ordinal))
                _badgeText.text = expired;
            return;
        }

        long days = Math.Max(1L, (seconds + 86399L) / 86400L);
        string label = LocalizationUtils.Format(
            "UI/Shop/DaysRemaining",
            "ОСТАЛОСЬ {0} Д.",
            days);
        if (!string.Equals(_badgeText.text, label, StringComparison.Ordinal))
            _badgeText.text = label;
    }

    private void EnsureDecorations()
    {
        if (_badgeBackground == null)
        {
            var badge = new GameObject(
                "OfferBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            badge.transform.SetParent(transform, false);
            var rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.72f, 0.83f);
            rect.anchorMax = new Vector2(0.98f, 0.98f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _badgeBackground = badge.GetComponent<Image>();
            _badgeBackground.sprite = _buyButton != null ? (_buyButton.targetGraphic as Image)?.sprite : null;
            _badgeBackground.type = _badgeBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            _badgeBackground.color = new Color(1f, 0.18f, 0.12f, 1f);
            _badgeBackground.raycastTarget = false;

            _badgeText = CreateRuntimeText(
                badge.transform,
                "Text",
                Vector2.zero,
                Vector2.one,
                18f,
                TextAlignmentOptions.Center,
                Color.white);
        }

    }

    private TextMeshProUGUI CreateRuntimeText(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float maxSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        var textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(Outline));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(3f, 2f);
        rect.offsetMax = new Vector2(-3f, -2f);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = _name != null ? _name.font : null;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = maxSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        var outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.01f, 0.01f, 0.96f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private void ConfigureEternalTrack(ShopPackData pack)
    {
        if (_eternalTrack == null)
        {
            var trackObject = new GameObject("EternalTrack", typeof(RectTransform));
            trackObject.transform.SetParent(transform, false);
            _eternalTrack = trackObject.AddComponent<SpecialShopEternalTrackView>();
        }

        _eternalTrack.gameObject.SetActive(true);
        _eternalTrack.Initialize(
            _shop,
            pack,
            _cardBackground != null ? _cardBackground.sprite : null,
            _name != null ? _name.font : null);
    }

    private static bool ShouldShowDescription(ShopPackData pack)
    {
        return pack != null &&
               pack.OfferStyle == ShopOfferStyle.MonthlyPass;
    }

    private static bool RequiresPeriodicRefresh(ShopPackData pack)
    {
        if (pack == null)
            return false;
        if (pack.AvailabilityDays > 0)
            return true;

        return HasTimerReward(pack);
    }

    private static bool HasTimerReward(ShopPackData pack)
    {
        if (pack == null)
            return false;

        if (pack.Rewards == null)
            return false;

        for (int i = 0; i < pack.Rewards.Count; i++)
        {
            switch (pack.Rewards[i].Type)
            {
                case ShopRewardType.ConsumableIncomeBoost:
                case ShopRewardType.TimedIncomeBoost:
                case ShopRewardType.ConsumableElementLuckBoost:
                case ShopRewardType.TimedElementLuckBoost:
                case ShopRewardType.ConsumableHatchSpeedBoost:
                case ShopRewardType.ConsumableOmniBoost:
                    return true;
            }
        }

        return false;
    }

    private static float GetPreferredHeight(ShopOfferStyle style)
    {
        switch (style)
        {
            case ShopOfferStyle.LimitedEgg:
            case ShopOfferStyle.MonthlyPass:
                return 320f;
            case ShopOfferStyle.EternalPack:
                return 300f;
            case ShopOfferStyle.TimedIncome:
            case ShopOfferStyle.TimedLuck:
                return 220f;
            case ShopOfferStyle.PremiumCurrency:
            case ShopOfferStyle.SoftCurrency:
                return 270f;
            case ShopOfferStyle.Potion:
            case ShopOfferStyle.Permanent:
                return 285f;
            default:
                return 280f;
        }
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureText(TextMeshProUGUI text, float minSize, float maxSize, TextAlignmentOptions alignment, TextOverflowModes overflow)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = overflow;
        text.raycastTarget = false;
    }
}

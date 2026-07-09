using MirraGames.SDK.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShop : MonoBehaviour
{
    [SerializeField] private List<ShopPackData> _packs;
    [SerializeField] private LocalizationData _localizationData;
    [SerializeField] private GameObject _shopCanvas;
    [SerializeField] private Transform _content;
    [SerializeField] private SpecialShopSlot _slotPrefab;
    [SerializeField] private ShopRow _rowPrefab;
    [SerializeField] private int _maxItemsPerRow = 2;
    [SerializeField] private ShopCategory _defaultCategory = ShopCategory.Featured;
    [SerializeField] private List<Button> _categoryButtons = new();
    [SerializeField] private Color _selectedTabColor = new Color(0.2f, 0.84f, 0.08f, 1f);
    [SerializeField] private Color _normalTabColor = new Color(0.31f, 0.16f, 0.07f, 1f);

    private readonly Dictionary<string, ShopPackData> _purchaseData = new();
    private readonly List<string> _pendingRestoredPurchaseIds = new();
    private readonly List<ShopRow> _rows = new();
    private readonly List<SpecialShopSlot> _slots = new();
    private ShopEffectsService _effects;
    private ShopCategory _currentCategory;
    private bool _isOpen;
    private bool _slotsInitialized;

    public bool Opened => _isOpen;
    public ShopCategory CurrentCategory => _currentCategory;
    public ShopEffectsService Effects => _effects;

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
    }

    private void Start()
    {
        _effects?.RestoreOwnedPermanentEffects(_packs);
        InitSlots();
        G.Purchases?.RestorePurchases();
    }

    private void OnEnable()
    {
        if (_effects != null)
            _effects.Changed += RefreshSlots;
        if (G.Currency != null)
        {
            G.Currency.NoGems.RemoveListener(Open);
            G.Currency.NoGems.AddListener(Open);
        }
    }

    private void OnDisable()
    {
        if (_effects != null)
            _effects.Changed -= RefreshSlots;
        if (G.Currency != null)
            G.Currency.NoGems.RemoveListener(Open);
    }

    private void OnDestroy()
    {
        if (G.SpecialShop == this)
            G.SpecialShop = null;
    }

    public void InitSlots()
    {
        ClearSlots();
        _slotsInitialized = false;
        _purchaseData.Clear();

        if (_content == null || _slotPrefab == null || _rowPrefab == null)
        {
            Debug.LogError("[SpecialShop] Content, slot prefab or row prefab is missing.");
            return;
        }

        var visible = GetVisiblePacks();
        for (int i = 0; i < visible.Count; i++)
        {
            var item = visible[i];
            Transform row = GetOrCreateAvailableRow(item.SlotType == ShopSlotType.Big);
            var slot = Instantiate(_slotPrefab, row);
            string purchaseId = GetPurchaseId(item);
            PurchaseData data = G.Purchases != null
                ? G.Purchases.GetPurchaseData(purchaseId)
                : PurchaseData.Fallback(purchaseId);

            if (!_purchaseData.ContainsKey(purchaseId))
                _purchaseData.Add(purchaseId, item);

            slot.Init(this, item, data);
            _slots.Add(slot);
        }

        _slotsInitialized = true;
        UpdateCategoryButtons();
        FlushPendingRestores();
        Canvas.ForceUpdateCanvases();
    }

    public void ShowFeatured() => SetCategory(ShopCategory.Featured);
    public void ShowBoosts() => SetCategory(ShopCategory.Boosts);
    public void ShowPermanent() => SetCategory(ShopCategory.Permanent);
    public void ShowCurrency() => SetCategory(ShopCategory.Currency);

    public void SetCategory(ShopCategory category)
    {
        if (_currentCategory == category && _slotsInitialized)
            return;

        _currentCategory = category;
        InitSlots();
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
        if (_shopCanvas == null)
            return;

        _isOpen = true;
        _shopCanvas.SetActive(true);
        if (G.Control != null)
            G.Control.CursorActive = true;
        G.Currency?.ShowGems?.Invoke(true);
        G.Input?.AOpenWindow?.Invoke(this);
        RefreshSlots();
    }

    public void Close()
    {
        if (_shopCanvas == null)
            return;

        _isOpen = false;
        _shopCanvas.SetActive(false);
        if (G.Control != null)
            G.Control.CursorActive = false;
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
    }

    public void TryBuy(PurchaseData purchaseData, ShopPackData packData)
    {
        if (packData == null)
            return;

        if (_effects != null && _effects.IsPermanentPackOwned(packData))
        {
            RefreshSlots();
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
        RefreshSlots();
        return result;
    }

    public string BuildRewardSummary(ShopPackData pack)
    {
        if (pack == null || pack.Rewards == null || pack.Rewards.Count == 0)
            return string.Empty;

        var reward = pack.Rewards[0];
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

    private List<ShopPackData> GetVisiblePacks()
    {
        var result = new List<ShopPackData>();
        if (_packs == null)
            return result;

        for (int i = 0; i < _packs.Count; i++)
        {
            var pack = _packs[i];
            if (pack == null)
                continue;

            bool visible = _currentCategory == ShopCategory.Featured
                ? pack.Featured
                : pack.Category == _currentCategory;
            if (visible)
                result.Add(pack);
        }

        result.Sort((a, b) =>
        {
            int sort = a.SortOrder.CompareTo(b.SortOrder);
            return sort != 0 ? sort : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        });
        return result;
    }

    private Transform GetOrCreateAvailableRow(bool big)
    {
        if (!big)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null && _rows[i].ItemsCount < _rows[i].MaxItems)
                    return _rows[i].transform;
            }
        }

        var newRow = Instantiate(_rowPrefab, _content);
        newRow.setMaxItems(big ? 1 : Mathf.Max(1, _maxItemsPerRow));
        _rows.Add(newRow);
        return newRow.transform;
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

        RefreshSlots();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] == null)
                continue;
            _rows[i].gameObject.SetActive(false);
            Destroy(_rows[i].gameObject);
        }

        _rows.Clear();
        _slots.Clear();
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
            if (button == null || button.targetGraphic == null)
                continue;

            button.targetGraphic.color = i == (int)_currentCategory ? _selectedTabColor : _normalTabColor;
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

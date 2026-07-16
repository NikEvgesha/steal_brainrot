using MirraGames.SDK.Common;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShopSlot : MonoBehaviour
{
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

        if (_name != null)
            _name.text = LocalizationUtils.T(pack.Name, pack.Name);
        if (_description != null)
            _description.text = LocalizationUtils.T(pack.DescriptionKey, pack.DescriptionFallback);
        bool hasMultipleRewards = pack.Rewards != null && pack.Rewards.Count > 1;
        if (_effectText != null)
        {
            _effectText.gameObject.SetActive(!hasMultipleRewards);
            _effectText.text = _shop != null ? _shop.BuildRewardSummary(pack) : string.Empty;
        }

        var icon = ResolveProductIcon(pack);
        if (_productIcon != null)
        {
            _productIcon.sprite = icon;
            _productIcon.enabled = icon != null;
            _productIcon.preserveAspect = true;
        }

        if (_cardBackground != null)
            _cardBackground.color = pack.AccentColor;

        BuildRewardIcons(pack);
        RefreshPrice();
        RefreshState();
    }

    private void Update()
    {
        if (_shopPackData == null || !_shopPackData.HasConsumableReward || Time.unscaledTime < _nextTimerRefresh)
            return;

        _nextTimerRefresh = Time.unscaledTime + 0.5f;
        RefreshState();
    }

    public void RefreshState()
    {
        if (_shopPackData == null)
            return;

        var effects = _shop != null ? _shop.Effects : null;
        bool permanentOwned = effects != null && effects.IsPermanentPackOwned(_shopPackData);
        int owned = effects != null ? effects.GetOwnedCount(_shopPackData) : 0;

        if (_buyButton != null)
            _buyButton.interactable = !permanentOwned;
        if (_price != null && permanentOwned)
            _price.text = LocalizationUtils.T("UI/Shop/Owned", "Owned");
        else
            RefreshPrice();

        if (_useButton != null)
        {
            _useButton.gameObject.SetActive(_shopPackData.HasConsumableReward);
            _useButton.interactable = owned > 0;
        }

        if (_useButtonText != null)
            _useButtonText.text = LocalizationUtils.T("UI/Shop/Use", "Use");
        if (_ownedText != null)
        {
            _ownedText.gameObject.SetActive(_shopPackData.HasConsumableReward);
            _ownedText.text = LocalizationUtils.Format("UI/Shop/OwnedCount", "Owned: {0}", owned);
        }

        RefreshActiveTimer(effects);
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
        string providerPrice = _productData != null ? _productData.Price : string.Empty;
        if (_price != null)
        {
            if (rewardedAdFallback)
                _price.text = LocalizationUtils.Format("UI/Shop/RewardedAdPrice", "+{0}", _shopPackData.RewardedAdGems);
            else
                _price.text = realPurchase && !string.IsNullOrWhiteSpace(providerPrice)
                    ? providerPrice
                    : _shopPackData.Price.ToString();
        }

        if (_currencyIcon != null)
        {
            Sprite icon = rewardedAdFallback
                ? _shop.RewardedAdIcon
                : G.Currency != null ? G.Currency.GetCurrencyIcon(_shopPackData.PriceCurrencyType) : null;
            _currencyIcon.sprite = icon;
            _currencyIcon.enabled = icon != null;
        }

        if (_currencyText != null)
            _currencyText.text = rewardedAdFallback || realPurchase
                ? string.Empty
                : LocalizationUtils.T(
                    "UI/Currency/" + _shopPackData.PriceCurrencyType,
                    _shopPackData.PriceCurrencyType.ToString());
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
                if (type == ShopRewardType.ConsumableIncomeBoost || type == ShopRewardType.ConsumableElementLuckBoost)
                {
                    seconds = effects.GetRemainingSeconds(type);
                    break;
                }
            }
        }

        _activeTimerText.gameObject.SetActive(seconds > 0);
        if (seconds > 0)
            _activeTimerText.text = LocalizationUtils.Format("UI/Shop/ActiveTimer", "Active: {0}", TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"));
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
        if (reward.Type == ShopRewardType.Item && reward.Item != null)
            return reward.Item.Icon;
        return reward.Icon;
    }
}

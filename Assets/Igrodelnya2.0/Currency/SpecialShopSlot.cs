using MirraGames.SDK;
using MirraGames.SDK.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShopSlot : MonoBehaviour
{
    [SerializeField] protected TextMeshProUGUI _name;
    [SerializeField] protected TextMeshProUGUI _price;
    [SerializeField] protected Image _currencyIcon;
    [SerializeField] protected TextMeshProUGUI _currencyText;
    [SerializeField] protected SpecialShopRewardSlot _rewardPrefab;
    [SerializeField] protected Transform _rewardParent;

    ShopPackData _shopPackData;
    PurchaseData _productData;

    private void OnEnable()
    {
        /*if (_itemData != null)
            _name.text = _name.text = LocalizationManager.Instance.LocalizationData.GetTranslation(_itemData.Name, LocalizationManager.Instance.CurrentLanguage, LocalizationKeyType.Item.ToString());*/
    }
    public void Init(ShopPackData pack, PurchaseData purchaseData)
    {
        if (pack == null)
        {
            Debug.LogWarning("[SpecialShopSlot] Cannot init slot: pack is missing.");
            gameObject.SetActive(false);
            return;
        }

        _shopPackData = pack;
        _productData = purchaseData;
        _name.text = LocalizationUtils.T(pack.Name, pack.Name);
        _price.text = purchaseData != null && !string.IsNullOrWhiteSpace(purchaseData.Price) ? purchaseData.Price : pack.Price.ToString();
        _currencyIcon.sprite = G.Currency.GetCurrencyIcon(pack.PriceCurrencyType);

        foreach (ShopReward reward in pack.Rewards)
        {
            SpecialShopRewardSlot r = Instantiate(_rewardPrefab, _rewardParent);
            Sprite icon = reward.Type == ShopRewardType.Item ? reward.Item.Icon : reward.Icon;
            r.SetReward(icon, reward.Amount);
        }

        BlockyUITheme.StyleShopProductCard(gameObject);

    }

    public void OnClick()
    {
        //MirraSDK.Payments.Purchase(
        //    productTag: "exampleProduct",
        //    onSuccess: () => {
        //        Debug.Log("Товар успешно куплен");
        //        // Выдать товар игроку
        //    },
        //    onError: () => Debug.Log("Товар не был куплен"),
        //);

        if (G.SpecialShop == null)
        {
            Debug.LogWarning("[SpecialShopSlot] Cannot buy pack: SpecialShop is not ready.");
            return;
        }

        G.SpecialShop.TryBuy(_productData, _shopPackData);
    }

}

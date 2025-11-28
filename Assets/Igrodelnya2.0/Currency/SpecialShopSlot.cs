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
        _shopPackData = pack;
        _productData = purchaseData;
        _name.text = pack.Name; //LocalizationManager.Instance.LocalizationData.GetTranslation(itemData.Name, LocalizationManager.Instance.CurrentLanguage, LocalizationKeyType.Item.ToString());
        _price.text = pack.Price.ToString(); //purchaseData.GetFullPriceInteger();
        _currencyIcon.sprite = G.Currency.GetCurrencyIcon(pack.PriceCurrencyType);

        foreach (ShopReward reward in pack.Rewards)
        {
            SpecialShopRewardSlot r = Instantiate(_rewardPrefab, _rewardParent);
            Sprite icon = reward.Type == ShopRewardType.Item ? reward.Item.Icon : reward.Icon;
            r.SetReward(icon, reward.Amount);
        }

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

        G.SpecialShop.TryBuy(_productData, _shopPackData);
    }

}

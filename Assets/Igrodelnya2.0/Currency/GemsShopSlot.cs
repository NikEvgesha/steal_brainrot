
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GemsShopSlot : SpecialShopSlot
{
    //[SerializeField] private TextMeshProUGUI _currencyText;

    ShopPackData _packData;
    PurchaseData _purchaseData;
    private GemsShop _shop;
    public void Init(ShopPackData packData, PurchaseData purchaseData, GemsShop shop)
    {
        //base.Init(packData.Name, packData.IMG, purchaseData.Price, packData.PriceCurrencyType);
        _packData = packData;
        _purchaseData = purchaseData;
        _shop = shop;
        _currencyIcon.gameObject.SetActive(false);
        _currencyText.text = _purchaseData.CurrencyImageURL;
    }

    //public override void OnClick()
    //{
    //    _shop.TryBuy(_purchaseData, _packData);
    //}

    public void InitImage(Sprite image)
    {
        _currencyIcon.sprite = image;
    }

}

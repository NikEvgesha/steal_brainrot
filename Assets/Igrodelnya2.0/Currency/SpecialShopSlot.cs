using UnityEngine;
using UnityEngine.UI;

public abstract class SpecialShopSlot : MonoBehaviour
{
    [SerializeField] private Text _name;
    [SerializeField] private Text _price;
    [SerializeField] private Image _icon;
    [SerializeField] protected Image _currencyIcon;

    private ItemData _itemData;

    private void OnEnable()
    {
        if (_itemData != null)
            _name.text = _name.text = LocalizationManager.Instance.LocalizationData.GetTranslation(_itemData.Name, LocalizationManager.Instance.CurrentLanguage, LocalizationKeyType.Item.ToString());
    }
    public void Init(ItemData itemData, string price, CurrencyType type)
    {
        _itemData = itemData;
        _name.text = _name.text = LocalizationManager.Instance.LocalizationData.GetTranslation(itemData.Name, LocalizationManager.Instance.CurrentLanguage, LocalizationKeyType.Item.ToString());
        _icon.sprite = itemData.IMG;

        _price.text = price;
        _currencyIcon.sprite = G.Currency.GetCurrencyIcon(type);
    }

    public abstract void OnClick();

}

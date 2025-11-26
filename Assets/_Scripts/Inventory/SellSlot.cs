using UnityEngine;
using UnityEngine.UI;

public class SellSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _price;
    [SerializeField] private Image _background;
    [SerializeField] private Button _sellButton;
    [SerializeField] private GameObject _lockSellIndicator;

    private InventoryItem _item;
    public InventoryItem Item => _item;

    public void Init(InventoryItem item)
    {
        _item = item;
        _icon.sprite = _item.Icon;
        _price.text = "$" + _item.BaseSellPrice; // TODO: use multipliers
        _sellButton.enabled = _item.SellAllowed;
        _lockSellIndicator.SetActive(!_item.SellAllowed);

        switch (item.RareType)
        {
            case RareType.Common:
                _background.color = Color.gray;
                break;
            case RareType.Uncommon:
                _background.color = Color.green;
                break;
            case RareType.Rare:
                _background.color = Color.blue;
                break;
            case RareType.Epic:
                _background.color = Color.yellow;
                break;
            case RareType.Legendary:
                _background.color = Color.magenta;
                break;
            case RareType.Mythic:
                _background.color = Color.red;
                break;
            default:
                break;
        }
    }

    public void _OnSellButtonClick()
    {
        if (!_item.SellAllowed) return;
        G.Currency.AddCurrency(CurrencyType.Coins, _item.BaseSellPrice);
        G.Inventory.Remove(_item);
        Destroy(gameObject);
    }
}

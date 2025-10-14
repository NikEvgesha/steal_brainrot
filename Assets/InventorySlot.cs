using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _name;
    [SerializeField] private GameObject _quickSlotIndicator;
    [SerializeField] private Image _background;
    [SerializeField] private GameObject _sellButtonLockIcon;
    [SerializeField] private GameObject _sellButtonUnlockIcon;
    [SerializeField] private GameObject _lockSellIndicator;

    private InventoryItem _item;
    public InventoryItem Item => _item;

    public void Init(InventoryItem item)
    {
        _item = item;

        if (item == null)
        {
            _background.gameObject.SetActive(false);
            return;
        }

        _background.gameObject.SetActive(true);
        _icon.sprite = item.Icon;
        _name.text = item.Name;
        if (_item.InQuickAccess)
            _quickSlotIndicator.SetActive(true);


        // TODO: get color from?
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
        _sellButtonLockIcon.SetActive(_item.SellAllowed);
        _sellButtonUnlockIcon.SetActive(!_item.SellAllowed);
        _lockSellIndicator.SetActive(!_item.SellAllowed);
    }

    public void _OnClick() {
        if (!_item.InQuickAccess)
        {
            bool added = G.QuickAccess.Add(_item);
            _quickSlotIndicator.SetActive(added);
        } else
        {
            G.QuickAccess.Remove(_item);
            _quickSlotIndicator.SetActive(false);
        }
            
    }

    public void _OnSellLockButtonClick()
    {
        _item.SellAllowed = !_item.SellAllowed;

        _sellButtonLockIcon.SetActive(_item.SellAllowed);
        _sellButtonUnlockIcon.SetActive(!_item.SellAllowed);
        _lockSellIndicator.SetActive(!_item.SellAllowed);

    }

}

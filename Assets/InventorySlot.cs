using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _name;
    [SerializeField] private GameObject _quickSlotIndicator;

    private InventoryItem _item;

    public void Init(InventoryItem item)
    {
        _item = item;
        _icon.sprite = item.Icon;
        _name.text = item.Name;
        if (_item.InQuickAccess)
            _quickSlotIndicator.SetActive(true);
    }

    public void _OnClick() {
        if (!_item.InQuickAccess)
        {
            bool added = QuickAccessManager.Instance.Add(_item);
            _quickSlotIndicator.SetActive(added);
        } else
        {
            QuickAccessManager.Instance.Remove(_item);
            _quickSlotIndicator.SetActive(false);
        }
            
    }


}

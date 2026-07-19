using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private GameObject _quickSlotIndicator;
    [SerializeField] private Image _background;
    [SerializeField] private GameObject _sellButtonLockIcon;
    [SerializeField] private GameObject _sellButtonUnlockIcon;
    [SerializeField] private GameObject _lockSellIndicator;

    private InventoryItem _item;
    public InventoryItem Item => _item;

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

        RefreshName();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    public void Init(InventoryItem item)
    {
        _item = item;

        if (item == null)
        {
            _quickSlotIndicator.SetActive(false);
            _background.gameObject.SetActive(false);
            return;
        }

        _background.gameObject.SetActive(true);
        _icon.sprite = item.Icon;
        RefreshName();
        _quickSlotIndicator.SetActive(_item.InQuickAccess);

        _sellButtonLockIcon.SetActive(_item.SellAllowed);
        _sellButtonUnlockIcon.SetActive(!_item.SellAllowed);
        _lockSellIndicator.SetActive(!_item.SellAllowed);
    }

    private void OnLanguageChanged(string _)
    {
        RefreshName();
    }

    private void RefreshName()
    {
        if (_name == null || _item == null)
            return;

        _name.text = ItemDisplayNameResolver.ResolveItemName(_item.Name, _item.gameObject.name);
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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private GridLayoutGroup _gridContent;
    [SerializeField] private InventorySlot _slotPrefab;
    [SerializeField] private GameObject _uiPanel;

    private List<InventorySlot> _slots = new List<InventorySlot>();
    private bool _isOpen;
    private Vector2 _slotSize;
    private int _columns;
    private Item _selectedTab = Item.Brainrot;


    public UnityEvent<InventoryItem> QuickAccessSwitched;

    public bool IsOpen => _isOpen;
    public Item SelectedTab => _selectedTab;
    public IReadOnlyList<InventorySlot> Slots => _slots;

    public InventorySlot FindSlot(InventoryItem item)
    {
        if (item == null)
            return null;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].Item == item)
                return _slots[i];
        }

        return null;
    }


    private void Awake()
    {
        RectTransform rect = _gridContent.GetComponent<RectTransform>();
        Debug.Log(" width: " + rect.rect.width);
        Debug.Log("cell width: " + _gridContent.cellSize.x);
        _columns = (int) (rect.rect.width / _gridContent.cellSize.x);
        Debug.Log("columns in inventory: " + _columns);
    }

    private void Start()
    {
        for (int i = 0; i < _columns; i++)
        {
            InventorySlot slot = Instantiate(_slotPrefab, _gridContent.transform);
            slot.Init(null);
            _slots.Add(slot);
        }

        ApplyBlockyStyle();
    }


    public void _ToggleOpen()
    {
        _isOpen = !_isOpen;
        _uiPanel.SetActive(_isOpen);
        _selectedTab = Item.Brainrot;
        UpdateItems(G.Inventory.GetItems(Item.Brainrot));
        if (_isOpen)
            ApplyBlockyStyle();
    }

    public void _ShowBrainrots()
    {
        _selectedTab = Item.Brainrot;
        UpdateItems(G.Inventory.GetItems(Item.Brainrot));
        ApplyBlockyStyle();
    }
    public void _ShowEggs()
    {
        _selectedTab = Item.Egg;
        UpdateItems(G.Inventory.GetItems(Item.Egg));
        ApplyBlockyStyle();
    }
    public void _ShowFood()
    {
        _selectedTab = Item.Food;
        UpdateItems(G.Inventory.GetItems(Item.Food));
        ApplyBlockyStyle();
    }


    public void _Show(Item type)
    {

    }

    private void UpdateItems(ReadOnlyCollection<InventoryItem> items)
    {
        int i = 0;
        for (; i < _slots.Count; i++)
        {
            if (i >= items.Count)
            {

                if (i >= items.Count + _columns - (items.Count % _columns))
                {
                    Destroy(_slots[i].gameObject);
                    _slots.RemoveAt(i);
                    i--;
                } else
                {
                    _slots[i].Init(null);
                }
                continue;
            }

            if (_slots[i].Item != items[i])
            {
                _slots[i].Init(items[i]);
            }
        }

        for (;i < items.Count; i++)
        {
            InventorySlot slot = Instantiate(_slotPrefab, _gridContent.transform);
            slot.Init(items[i]);
            _slots.Add(slot);
        }

    }

    private void ApplyBlockyStyle()
    {
        BlockyUITheme.StyleInventoryWindow(_uiPanel, _selectedTab);
    }

}

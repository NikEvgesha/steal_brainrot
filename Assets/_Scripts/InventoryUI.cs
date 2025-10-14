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


    public UnityEvent<InventoryItem> QuickAccessSwitched;


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
    }


    public void _ToggleOpen()
    {
        _isOpen = !_isOpen;
        _uiPanel.SetActive(_isOpen);
        UpdateItems(G.Inventory.GetItems(Item.Brainrot));
    }

    public void _ShowBrainrots()
    {
        UpdateItems(G.Inventory.GetItems(Item.Brainrot));
    }
    public void _ShowEggs()
    {
        UpdateItems(G.Inventory.GetItems(Item.Egg));
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

}

using System.Collections.Generic;
using UnityEngine;

public class QuickAccessPanelUI : MonoBehaviour
{
    [SerializeField] private QuickSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;

    private List<QuickSlot> _slots;

    void Start()
    {
        G.QuickAccess.ItemsUpdated.AddListener(UpdateUI);

        _slots = new List<QuickSlot>();

        for (int i = 0; i < G.QuickAccess.Capacity; i++)
        {
            QuickSlot slot = Instantiate(_slotPrefab, _slotParent);
            _slots.Add(slot);
            slot.SetIndex(i + 1);
        }

        G.QuickAccess.OnUIInitialized();
    }

    private void UpdateUI(List<InventoryItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            _slots[i].gameObject.SetActive(true);
            _slots[i].Init(items[i]);
        }

        if (items.Count < _slots.Count)
        {
            for (int i = items.Count; i < _slots.Count; i++)
            {
                _slots[i].Init(null);
                _slots[i].gameObject.SetActive(false);
            }
        }

    }

}

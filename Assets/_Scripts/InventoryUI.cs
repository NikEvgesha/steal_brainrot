using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private DynamicGridSpawner _grid;
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private GameObject _uiPanel;

    private bool _isOpen;

    public UnityEvent<InventoryItem> QuickAccessSwitched;



    public void _ToggleOpen()
    {
        _isOpen = !_isOpen;
        _uiPanel.SetActive(_isOpen);
        UpdateItems(Inventory.Instance.GetItems(Item.Brainrot));
    }

    public void _ShowBrainrots()
    {
        UpdateItems(Inventory.Instance.GetItems(Item.Brainrot));
    }
    public void _ShowEggs()
    {
        UpdateItems(Inventory.Instance.GetItems(Item.Egg));
    }


    private void UpdateItems(ReadOnlyCollection<InventoryItem> items)
    {
        while (_grid.transform.childCount > 0)
        {
            DestroyImmediate(_grid.transform.GetChild(0).gameObject);
        }

        foreach (InventoryItem item in items)
        {
            InventorySlot slot = _grid.SpawnObject<InventorySlot>(_slotPrefab);
            slot.Init(item);
        }
    } 

}

using System.Collections.ObjectModel;
using UnityEngine;

public class SellPoint : MonoBehaviour
{
    [SerializeField] private GameObject _ui;
    [SerializeField] private Transform _animalsParent;
    [SerializeField] private Transform _eggsParent;
    [SerializeField] private SellSlot _slotPrefab;

    [SerializeField] private Transform  _teleportPoint;

    public Transform TeleportPoint => _teleportPoint;

    private void Open()
    {
        ReadOnlyCollection<InventoryItem> _animals = G.Inventory.GetItems(Item.Brainrot);
        ReadOnlyCollection<InventoryItem> _eggs = G.Inventory.GetItems(Item.Egg);

        foreach (InventoryItem item in _animals)
        {
            SellSlot slot = Instantiate(_slotPrefab, _animalsParent);
            slot.Init(item);
        }

        foreach (InventoryItem item in _eggs)
        {
            SellSlot slot = Instantiate(_slotPrefab, _eggsParent);
            slot.Init(item);
        }

        _ui.SetActive(true);
        if (_eggsParent != null && _eggsParent.TryGetComponent<AdaptiveGridSpawner>(out var eggsGrid))
            eggsGrid.Rebuild();
    }


    private void Close()
    {
        while (_animalsParent.childCount > 0)
        {
            DestroyImmediate(_animalsParent.GetChild(0).gameObject);
        }
        while (_eggsParent.childCount > 0)
        {
            DestroyImmediate(_eggsParent.GetChild(0).gameObject);
        }
        _ui.SetActive(false);
    }



    public void _OnPlayerEnter()
    {
        Open();
    }


    public void _OnPlayerExit()
    {
        Close();
    }
}

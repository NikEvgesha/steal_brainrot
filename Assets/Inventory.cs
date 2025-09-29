using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;

public class Inventory : MonoBehaviour
{
    private static Inventory _instance;
    public static Inventory Instance { get { return _instance; } }

    private List<InventoryItem> _brainrots;
    private List<InventoryItem> _eggs;
    private InventoryUI _ui;

    public UnityEvent<Item, List<InventoryItem>> ItemsUpdated;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
        _brainrots = new();
        _eggs = new();
    }

    private void Start()
    {
        _brainrots = new();
        _eggs = new();
        _ui = FindAnyObjectByType<InventoryUI>();
        _ui.QuickAccessSwitched.AddListener(TrySwitchQuickAccessStatus);
    }



    public void Add(InventoryItem item)
    {
        switch (item.Type)
        {
            case Item.Brainrot:
                _brainrots.Add(item);
                break;
            case Item.Egg:
                _eggs.Add(item);
                break;
            default:
                break;
        }
        item.transform.SetParent(PlayerManager.Instance.transform);
        item.transform.localPosition = Vector3.up;
        item.gameObject.SetActive(false);
        QuickAccessManager.Instance.Add(item);

    }

    public void Remove(InventoryItem item)
    {
        switch (item.Type)
        {
            case Item.Brainrot:
                _brainrots.Remove(item);
                break;
            case Item.Egg:
                _eggs.Remove(item);
                break;
            default:
                break;
        }
        QuickAccessManager.Instance.Remove(item);
    }

    private void TrySwitchQuickAccessStatus(InventoryItem item)
    {

    }

    public ReadOnlyCollection<InventoryItem> GetItems(Item type)
    {
        switch (type)
        {
            case Item.Brainrot:
                return _brainrots.AsReadOnly();
            case Item.Egg:
                return _eggs.AsReadOnly();
            default:
                return null;
        }
    }


}

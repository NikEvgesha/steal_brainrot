using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;

public class Inventory : MonoBehaviour
{
    private List<InventoryItem> _brainrots;
    private List<InventoryItem> _eggs;
    private InventoryUI _ui;

    public UnityEvent<Item, List<InventoryItem>> ItemsUpdated;

    private void Awake()
    {
        if (G.Inventory == null)
        {
            G.Inventory = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(this);
        }
        _brainrots = new();
        _eggs = new();
    }

    public void Init()
    {
        _brainrots = new();
        _eggs = new();
        //_ui = FindAnyObjectByType<InventoryUI>();
        //_ui.QuickAccessSwitched.AddListener(TrySwitchQuickAccessStatus);
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
        item.transform.SetParent(G.Player.transform);
        item.gameObject.SetActive(false);
        item.SellAllowed = true;
        item.OnInventoryAdd();
        G.QuickAccess.Add(item);
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
        G.QuickAccess.Remove(item);
    }

    public void TrySwitchQuickAccessStatus(InventoryItem item)
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

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TestJson {
    public string name;
    public int id;
}

[System.Serializable]
public class ItemSaveData
{
    public Item Status;
    public string ID;
    public BrainrotDinamicData DinamicData;
    public ItemSaveData() { }
    public ItemSaveData(Item type, string id)
    {
        Status = type;
        ID = id;
    }
    public ItemSaveData(Item type, string id, BrainrotDinamicData data)
    {
        Status = type;
        ID = id;
        DinamicData = data;
    }
}

public class Inventory : MonoBehaviour
{
    private List<InventoryItem> _brainrots;
    private List<InventoryItem> _eggs;
    private List<InventoryItem> _food;
    private InventoryUI _ui;
    private List<ItemSaveData> _brainrotsSaveData;
    private List<ItemSaveData> _eggsSaveData;
    private List<ItemSaveData> _foodSaveData;

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
    }

    public void Init()
    {
        _brainrots = new();
        _eggs = new();
        _food = new();

        _brainrotsSaveData = G.Save.LoadInventory(Item.Brainrot);
        _foodSaveData = G.Save.LoadInventory(Item.Food);
        _eggsSaveData = G.Save.LoadInventory(Item.Egg);
        //_ui = FindAnyObjectByType<InventoryUI>();
        //_ui.QuickAccessSwitched.AddListener(TrySwitchQuickAccessStatus);

        AddLoadedItems();
    }

    public void Add(InventoryItem item, bool forceSave = true)
    {
        switch (item.Type)
        {
            case Item.Brainrot:
                _brainrots.Add(item);
                Brainrot pet = item.GetComponent<Brainrot>();
                
                if (forceSave)
                {
                    _brainrotsSaveData.Add(new ItemSaveData(item.Type, item.Name, pet.DinamicData));
                    G.Save.SaveInventory(item.Type, _brainrotsSaveData);
                }            
                break;
            case Item.Egg:
                _eggs.Add(item);
                Egg egg = item.GetComponent<Egg>();
                
                if (forceSave)
                {
                    _eggsSaveData.Add(new ItemSaveData(item.Type, item.Name, egg.Data.DinamicData));
                    G.Save.SaveInventory(item.Type, _eggsSaveData);
                }
                    
                break;
            case Item.Food:
                _food.Add(item);
                
                if (forceSave)
                {
                    _foodSaveData.Add(new ItemSaveData(item.Type, item.Name));
                    G.Save.SaveInventory(item.Type, _foodSaveData);
                }
                    
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
        int idx;
        switch (item.Type)
        {
            case Item.Brainrot:
                idx = _brainrots.IndexOf(item);
                _brainrots.Remove(item);
                _brainrotsSaveData.RemoveAt(idx);
                G.Save.SaveInventory(item.Type, _brainrotsSaveData);
                break;
            case Item.Egg:
                idx = _eggs.IndexOf(item);
                _eggs.Remove(item);
                _eggsSaveData.RemoveAt(idx);
                G.Save.SaveInventory(item.Type, _eggsSaveData);
                break;
            case Item.Food:
                idx = _food.IndexOf(item);
                _food.Remove(item);
                _foodSaveData.RemoveAt(idx);
                G.Save.SaveInventory(item.Type, _foodSaveData);
                break;
            default:
                break;
        }
        G.QuickAccess.Remove(item);
    }

    public ReadOnlyCollection<InventoryItem> GetItems(Item type)
    {
        switch (type)
        {
            case Item.Brainrot:
                return _brainrots.AsReadOnly();
            case Item.Egg:
                return _eggs.AsReadOnly();
            case Item.Food:
                return _food.AsReadOnly();
            default:
                return null;
        }
    }



    private void AddLoadedItems()
    {
        foreach (var item in _eggsSaveData)
        {
            Egg prefab = G.Storage.GetEgg(item.ID);
            if (prefab != null)
            {
                Egg egg = Instantiate(prefab);
                egg.SetData(item.DinamicData);
                Add(egg, false);
            }
        }

        foreach (var item in _foodSaveData)
        {
            Food prefab = G.Storage.GetFood(item.ID);
            if (prefab != null)
            {
                Food food = Instantiate(prefab);
                Add(food, false);
            }
        }

        foreach (var item in _brainrotsSaveData)
        {
            Brainrot prefab = G.Storage.GetPet(item.ID);
            if (prefab != null)
            {
                Brainrot pet = Instantiate(prefab);
                pet.Init(item.DinamicData);
                Add(pet, false);
            }
        }

    }


}

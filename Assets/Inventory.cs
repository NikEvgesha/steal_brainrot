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
    public UnityEvent<InventoryItem> ItemAdded = new UnityEvent<InventoryItem>();
    public UnityEvent<InventoryItem> ItemRemoved = new UnityEvent<InventoryItem>();
    public bool IsInitialized => _brainrots != null && _eggs != null && _food != null;

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
        ItemAdded?.Invoke(item);
        if (forceSave)
        {
            G.Sound?.Play(GameAudioId.SFX_ITEM_PICKUP);
            TutorialSignals.Raise(TutorialSignalType.ItemAcquired, item, item.Name, item.Type);
        }
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
        ItemRemoved?.Invoke(item);
    }

    public ReadOnlyCollection<InventoryItem> GetItems(Item type)
    {
        switch (type)
        {
            case Item.Brainrot:
                return _brainrots != null ? _brainrots.AsReadOnly() : null;
            case Item.Egg:
                return _eggs != null ? _eggs.AsReadOnly() : null;
            case Item.Food:
                return _food != null ? _food.AsReadOnly() : null;
            default:
                return null;
        }
    }



    private void AddLoadedItems()
    {
        if (G.Storage == null)
        {
            Debug.LogWarning("[Inventory] ItemPrefabStorage is not initialized. Saved inventory was not loaded.");
            return;
        }

        if (_eggsSaveData != null)
        {
            foreach (var item in _eggsSaveData)
            {
                if (item == null || string.IsNullOrEmpty(item.ID))
                    continue;

                Egg prefab = G.Storage.GetEgg(item.ID);
                if (prefab == null)
                    continue;

                Egg egg = Instantiate(prefab);
                egg.SetData(item.DinamicData);
                Add(egg, false);
            }
        }

        if (_foodSaveData != null)
        {
            foreach (var item in _foodSaveData)
            {
                if (item == null || string.IsNullOrEmpty(item.ID))
                    continue;

                Food prefab = G.Storage.GetFood(item.ID);
                if (prefab == null)
                    continue;

                Food food = Instantiate(prefab);
                Add(food, false);
            }
        }

        if (_brainrotsSaveData != null)
        {
            foreach (var item in _brainrotsSaveData)
            {
                if (item == null || string.IsNullOrEmpty(item.ID))
                    continue;

                Brainrot prefab = G.Storage.GetPet(item.ID);
                if (prefab == null)
                    continue;

                Brainrot pet = Instantiate(prefab);
                pet.Init(item.DinamicData);
                Add(pet, false);
            }
        }

    }


}

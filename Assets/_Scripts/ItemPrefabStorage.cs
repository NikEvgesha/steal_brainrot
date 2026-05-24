using MirraGames.SDK;
using System.Collections.Generic;
using UnityEngine;

public class ItemPrefabStorage : MonoBehaviour
{
    [SerializeField] private ItemsList _eggs;
    [SerializeField] private ItemsList _pets;
    [SerializeField] private ItemsList _food;

    private void Awake()
    {
        if (G.Storage == null)
        {
            G.Storage = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

    }

    public void Init()
    {
    }

    public Egg GetEgg(string name)
    {
        return GetPrefab<Egg>(_eggs, name, "egg");
    }

    public IReadOnlyList<Egg> GetAllEggPrefabs()
    {
        if (_eggs == null)
            return System.Array.Empty<Egg>();

        return _eggs.GetAllOfType<Egg>();
    }

    public Brainrot GetPet(string name)
    {
        return GetPrefab<Brainrot>(_pets, name, "pet");
    }

    public IReadOnlyList<Brainrot> GetAllPetPrefabs()
    {
        if (_pets == null)
            return System.Array.Empty<Brainrot>();

        return _pets.GetAllOfType<Brainrot>();
    }


    public Food GetFood(string name)
    {
        return GetPrefab<Food>(_food, name, "food");
    }

    private T GetPrefab<T>(ItemsList list, string itemName, string listName) where T : InventoryItem
    {
        if (list == null)
        {
            Debug.LogWarning($"[ItemPrefabStorage] Missing {listName} items list.");
            return null;
        }

        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning($"[ItemPrefabStorage] Empty {listName} prefab id.");
            return null;
        }

        InventoryItem item = list.GetByName(itemName);
        if (item == null)
        {
            Debug.LogWarning($"[ItemPrefabStorage] Missing {listName} prefab: '{itemName}'. The saved data may be outdated.");
            return null;
        }

        T prefab = item.GetComponent<T>();
        if (prefab == null)
            Debug.LogWarning($"[ItemPrefabStorage] Prefab '{itemName}' is not a {typeof(T).Name}.");

        return prefab;
    }
}

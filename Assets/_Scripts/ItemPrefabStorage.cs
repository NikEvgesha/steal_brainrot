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
        InventoryItem item = _eggs.GetByName(name);
        return item.GetComponent<Egg>();
    }

    public IReadOnlyList<Egg> GetAllEggPrefabs()
    {
        if (_eggs == null)
            return System.Array.Empty<Egg>();

        return _eggs.GetAllOfType<Egg>();
    }

    public Brainrot GetPet(string name)
    {
        InventoryItem item = _pets.GetByName(name);
        return item.GetComponent<Brainrot>();
    }

    public IReadOnlyList<Brainrot> GetAllPetPrefabs()
    {
        if (_pets == null)
            return System.Array.Empty<Brainrot>();

        return _pets.GetAllOfType<Brainrot>();
    }


    public Food GetFood(string name)
    {
        InventoryItem item = _food.GetByName(name);
        return item.GetComponent<Food>();
    }
}

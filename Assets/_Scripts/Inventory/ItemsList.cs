using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemList", menuName ="Scriptable/ItemList")]
public class ItemsList : ScriptableObject
{
    [SerializeField] private List<InventoryItem> _items;

    private Dictionary<string, InventoryItem> _dict;
    public IReadOnlyList<InventoryItem> Items => _items;


    public void Init()
    {
        _dict = new Dictionary<string, InventoryItem>();
        _items.ForEach(x => _dict.Add(x.Name, x));
    }

    public InventoryItem GetByName(string name)
    {

        if (_dict == null) Init();

        InventoryItem item = null;
        _dict.TryGetValue(name, out item);
        return item;
    }

    public List<T> GetAllOfType<T>() where T : InventoryItem
    {
        var result = new List<T>();
        if (_items == null || _items.Count == 0)
            return result;

        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i] is T typed && typed != null)
                result.Add(typed);
        }

        return result;
    }
}

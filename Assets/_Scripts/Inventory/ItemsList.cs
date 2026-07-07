using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemList", menuName ="Scriptable/ItemList")]
public class ItemsList : ScriptableObject
{
    [SerializeField] private List<InventoryItem> _items;

    private Dictionary<string, InventoryItem> _dict;
    public IReadOnlyList<InventoryItem> Items => _items;

    private void OnEnable()
    {
        _dict = null;
    }

    public void Init()
    {
        _dict = new Dictionary<string, InventoryItem>();

        if (_items == null)
            return;

        foreach (InventoryItem item in _items)
        {
            if (item == null)
            {
                Debug.LogWarning($"[ItemsList] Null item in '{name}'.");
                continue;
            }

            if (string.IsNullOrEmpty(item.Name))
            {
                Debug.LogWarning($"[ItemsList] Item with empty name in '{name}'.");
                continue;
            }

            if (_dict.ContainsKey(item.Name))
            {
                Debug.LogWarning($"[ItemsList] Duplicate item name '{item.Name}' in '{name}'.");
                continue;
            }

            _dict.Add(item.Name, item);
        }
    }

    public InventoryItem GetByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (_dict == null) Init();

        InventoryItem item = null;
        if (_dict.TryGetValue(name, out item))
            return item;

        // Asset references can be edited while the ScriptableObject instance stays loaded in the Editor.
        Init();
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

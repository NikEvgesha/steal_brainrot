using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemList", menuName ="Scriptable/ItemList")]
public class ItemsList : ScriptableObject
{
    [SerializeField] private List<InventoryItem> _items;

    private Dictionary<string, InventoryItem> _dict;


    private void Init()
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
}
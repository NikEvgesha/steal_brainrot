using UnityEngine;

public abstract class InventoryItem : MonoBehaviour
{
    [SerializeField] private string _name;
    [SerializeField] protected Item _type;
    [SerializeField] protected Sprite _icon;

    private protected bool _inQuickAccess;

    public Item Type => _type;
    public Sprite Icon => _icon;
    public string Name => _name;
    public bool InQuickAccess { get; set; }
}
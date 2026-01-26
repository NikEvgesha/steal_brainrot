using UnityEngine;

public abstract class InventoryItem : MonoBehaviour
{
    [SerializeField] private string _name;
    [SerializeField] protected Item _type;
    [SerializeField] protected Sprite _icon;
    [SerializeField] protected RareType _rareType;
    [SerializeField] protected double _baseSellPrice;

    private protected bool _inQuickAccess;
    private protected bool _sellAllowed = true;

    public Item Type => _type;
    public Sprite Icon => _icon;
    public string Name => _name;
    public bool InQuickAccess { get; set; }

    public RareType RareType => _rareType;

    public bool SellAllowed { get; set; }

    public double BaseSellPrice => _baseSellPrice;

    public virtual void OnInventoryAdd() {}

}
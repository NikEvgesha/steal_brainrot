using UnityEngine;

[CreateAssetMenu(menuName ="Scriptable/BrainrotData")]
public class BrainrotData : ScriptableObject
{
    [SerializeField] private string _name;
    [SerializeField] private GameObject _model;
    [SerializeField] private uint _buyPrice; // big int ? decimal ?
    [SerializeField] private uint _income; // in second
    [SerializeField] private float _weight;
    [SerializeField] private uint _sellPrice;

    public string Name
    {
        get
        {
            // localized name ? 
            return _name;
        }
    }

    public GameObject Model => _model;
    public uint BuyPrice => _buyPrice;
    public uint SellPrice => _sellPrice;
    public uint Income => _income;
    public float Weight => _weight;
}

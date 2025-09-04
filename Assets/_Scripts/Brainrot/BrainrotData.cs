using UnityEngine;

[CreateAssetMenu(menuName ="Scriptable/BrainrotData")]
public class BrainrotData : ScriptableObject
{
    [SerializeField] private string _name;
    [SerializeField] private GameObject _model;
    [SerializeField] private int _price; // big int ? decimal ?
    [SerializeField] private int _income; // in second
    [SerializeField] private float _weight;

    public string Name
    {
        get
        {
            // localized name ? 
            return _name;
        }
    }

    public GameObject Model => _model;
    public int Price => _price;
    public int Income => _income;
    public float Weight => _weight;
}

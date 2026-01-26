using UnityEngine;

[CreateAssetMenu(menuName ="Scriptable/BrainrotData")]
public class BrainrotData : ScriptableObject
{
    [SerializeField] private string _nameKey;
    [SerializeField] private RareType _rareType;
    [SerializeField] private GameObject _model;

    [SerializeField] private double _startIncome; // in second
    [SerializeField] private float _minWeight;
    [SerializeField] private float _maxWeightMult = 3F;

    [SerializeField] private double _startSellPrice;

    public string Name
    {
        get
        {
            // localized name ? 
            return _nameKey;
        }
    }

    public GameObject Model => _model;
    public double StartSellPrice => _startSellPrice;
    public double Income => _startIncome;
    public float MinWeight => _minWeight;
    public float MaxWeightMult => _maxWeightMult;
}

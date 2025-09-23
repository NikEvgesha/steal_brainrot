using UnityEngine;

[CreateAssetMenu(menuName ="Scriptable/BrainrotData")]
public class BrainrotData : ScriptableObject
{
    [SerializeField] private string _nameKey;
    [SerializeField] private RareType _rareType;
    [SerializeField] private GameObject _model;

    [SerializeField] private float _startIncome; // in second
    [SerializeField] private float _minWeight;
    [SerializeField] private float _maxWeightMult = 3F;

    [SerializeField] private float _startSellPrice;

    public string Name
    {
        get
        {
            // localized name ? 
            return _nameKey;
        }
    }

    public GameObject Model => _model;
    public float StartSellPrice => _startSellPrice;
    public float Income => _startIncome;
    public float MinWeight => _minWeight;
}

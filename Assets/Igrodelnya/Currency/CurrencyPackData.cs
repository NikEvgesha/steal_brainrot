using UnityEngine;

[CreateAssetMenu(fileName = "CurrencyPack", menuName = "ScriptableObject/CurrencyPackData")]
public class CurrencyPackData : ScriptableObject
{
    [SerializeField] private ItemData _data;
    [SerializeField] private int _amount;
    [SerializeField] private CurrencyType _rewardCurrencyType;
    [SerializeField] private int price;
    [SerializeField] private CurrencyType _priceCurrencyType;
    [SerializeField] private GameObject _model;

    public ItemData Data => _data;
    public int Amount => _amount;
    public CurrencyType CurrencyType => _rewardCurrencyType;
    public int Price => price;
    public CurrencyType PriceCurrencyType => _priceCurrencyType;

    public GameObject Model => _model;
}
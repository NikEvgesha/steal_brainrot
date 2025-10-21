using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct LevelEggs
{
    public Egg egg;
    public float weight;
}

public class ConveyorLevel : MonoBehaviour
{
    [SerializeField] private string _name;
    [SerializeField] private int _priceCoin;
    [SerializeField] private int _priceGems;
    [SerializeField] private Sprite _icon;
    [SerializeField] private float _incomeMultiplier;
    [SerializeField] private List<LevelEggs> _eggs;
    [SerializeField] private Egg _newEgg;
    [SerializeField] private bool _purchased;
    private float _totalWeight;
    private bool _active;


    public float IncomeMultiplier => _incomeMultiplier;
    public List<LevelEggs> Eggs => _eggs;
    public Egg NewEgg => _newEgg;

    public string Name => _name;
    public int PriceCoin => _priceCoin;
    public int PriceGems => _priceGems;
    public Sprite Icon => _icon;

    public bool IsPurchased => _purchased;
    public bool IsActive => _active;
    

    private void Awake()
    {
        _totalWeight = 0f;
        foreach(LevelEggs egg in _eggs)
        {
            _totalWeight += egg.weight;
        }
    }

    public Egg GetRandomEgg()
    {
        float rand = UnityEngine.Random.Range(0, _totalWeight);
        float current = 0f;
        for (int i = 0; i < _eggs.Count; i++)
        {
            current += _eggs[i].weight;
            if (current >= rand) 
                return _eggs[i].egg;
        }

        return _eggs[0].egg;

    }

    public void TryBuy(bool forGems)
    {
        if (_purchased) return;


        if (G.Currency.RemoveCurrency(forGems ? CurrencyType.Gems : CurrencyType.Coins, forGems ? _priceGems : _priceCoin))
        {
            _purchased = true;
        }
    }


    public void SetActive(bool active)
    {
        _active = active;
    }
}

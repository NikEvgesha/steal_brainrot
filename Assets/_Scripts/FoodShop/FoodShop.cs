using MirraGames.SDK;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class FoodShop : MonoBehaviour
{
    [SerializeField] private List<Food> _foodList;
    [SerializeField] private FoodShopUI _ui;
    [SerializeField] private int _resupplyTime;
    [SerializeField] private int _priceResupply;
    [SerializeField] private Text _updateTimer;

    private Dictionary<Food, int> _foodAmount;
    private int _timeToResupply;

    private void Awake()
    {
        _foodAmount = new Dictionary<Food, int>();
        foreach (var food in _foodList)
        {
            _foodAmount.Add(food, 0);
        }
    }

    private void Start()
    {
        Resupply();
        _ui.InitUI(_foodList, _priceResupply);
        _ui.BuyButtonClicked.AddListener(TryBuy);
        _ui.ResupplyButtonClicked.AddListener(TryBuyResupply); //TODO : buy for gems
    }


    private IEnumerator ResupplyTimer()
    {
        _timeToResupply = _resupplyTime;
        while (_timeToResupply >= 0)
        {
            _updateTimer.text = String.Format(
                    "{0}:{1}",
                    (_timeToResupply/60).ToString("D2"),
                    (_timeToResupply%60).ToString("D2")
                );
            _timeToResupply--;
            yield return new WaitForSecondsRealtime(1);
        }
        Resupply();
    }

    private void Resupply()
    {
        foreach (var food in _foodList)
        {
            float p = UnityEngine.Random.value;
            if (p <= food.Data.SupplyProbability)
            {
                _foodAmount[food] = food.Data.StockAmount;
            } else
            {
                _foodAmount[food] = 0;
            }
        }
        _ui.UpdateUI(_foodAmount);
        StopAllCoroutines();
        StartCoroutine(ResupplyTimer());
    }


    private void Open()
    {
        _ui.gameObject.SetActive(true);
    }


    private void Close()
    {
        _ui.gameObject.SetActive(false);
    }



    public void _OnPlayerEnter()
    {
        Open();
    }


    public void _OnPlayerExit()
    {
        Close();
    }


    private void TryBuy(Food food, bool forGems)
    {
        if (!_foodAmount.ContainsKey(food) || _foodAmount[food] == 0) return;

        if (G.Currency.RemoveCurrency(forGems ? CurrencyType.Gems : CurrencyType.Coins, forGems ? food.Data.GemPrice : food.Data.MoneyPrice))
        {
            Food foodObj = Instantiate(food);
            G.Inventory.Add(foodObj);
            _foodAmount[food]--;
            _ui.UpdateUI(_foodAmount);
        }
    }

    private void TryBuyResupply()
    {
        if (G.Currency.RemoveCurrency(CurrencyType.Gems, _priceResupply))
        {
            Resupply();
        }
    }
}

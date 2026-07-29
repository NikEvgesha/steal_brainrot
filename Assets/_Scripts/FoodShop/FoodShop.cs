using MirraGames.SDK;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class FoodShop : MonoBehaviour
{
    [SerializeField] private List<Food> _foodList;
    [SerializeField] private FoodShopUI _ui;
    [SerializeField] private int _resupplyTime;
    [SerializeField] private int _priceResupply;
    [SerializeField] private TMP_Text _updateTimer;
    [SerializeField] private Transform _teleportPoint;

    private Dictionary<Food, int> _foodAmount;
    private int _timeToResupply;
    private bool _playerInside;
    private AudioSource _shopAmbience;


    public Transform TeleportPoint => _teleportPoint;
    public IReadOnlyList<Food> Foods => _foodList;
    public FoodShopUI Ui => _ui;
    public bool IsPlayerInside => _playerInside;

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
        _ui.InitUI(_foodList, _priceResupply);
        _ui.BuyButtonClicked.AddListener(TryBuy);
        _ui.ResupplyButtonClicked.AddListener(TryBuyResupply); //TODO : buy for gems

        Resupply();
        _ui.UpdateUI(_foodAmount);
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
        if (_playerInside)
            G.Sound?.Play(GameAudioId.SFX_SHOP_RESTOCK);
    }


    private void Open()
    {
        _ui.gameObject.SetActive(true);
        G.Sound?.Play(GameAudioId.SFX_UI_OPEN);
        if (_shopAmbience == null)
            _shopAmbience = G.Sound?.PlayLoop(GameAudioId.AMB_FOOD_SHOP, transform);
    }


    private void Close()
    {
        _ui.gameObject.SetActive(false);
        G.Sound?.Play(GameAudioId.SFX_UI_CLOSE);
        if (_shopAmbience != null)
        {
            G.Sound?.StopLoop(_shopAmbience);
            _shopAmbience = null;
        }
    }



    public void _OnPlayerEnter()
    {
        _playerInside = true;
        Open();
    }


    public void _OnPlayerExit()
    {
        _playerInside = false;
        Close();
    }


    private void TryBuy(Food food, bool forGems)
    {
        CurrencyType currency = forGems ? CurrencyType.Gems : CurrencyType.Coins;
        double price = forGems ? food.Data.GemPrice : food.Data.MoneyPrice;
        int stockBefore = _foodAmount.TryGetValue(food, out int stock) ? stock : 0;
        if (!_foodAmount.ContainsKey(food) || _foodAmount[food] == 0)
        {
            G.Sound?.Play(GameAudioId.SFX_UI_LOCKED);
            TrackFoodPurchase(food, currency, price, stockBefore, stockBefore, "failed", "out_of_stock");
            return;
        }

        if (G.Currency.RemoveCurrency(currency, price))
        {
            Food foodObj = Instantiate(food);
            using (GameAnalytics.BeginItemGrant("food_shop", food.Name, currency.ToString().ToLowerInvariant(), price))
                G.Inventory.Add(foodObj);
            _foodAmount[food]--;
            _ui.UpdateUI(_foodAmount);
            TutorialSignals.Raise(TutorialSignalType.FoodPurchased, foodObj, foodObj.Name, Item.Food, food.Data.MoneyPrice);
            TrackFoodPurchase(food, currency, price, stockBefore, _foodAmount[food], "success", string.Empty);
        }
        else
            TrackFoodPurchase(food, currency, price, stockBefore, stockBefore, "failed", "insufficient_currency");
    }

    private static void TrackFoodPurchase(
        Food food,
        CurrencyType currency,
        double price,
        int stockBefore,
        int stockAfter,
        string result,
        string failureReason)
    {
        GameAnalytics.Track(AnalyticsEventNames.FoodPurchaseResult, GameAnalytics.Params(
            "food_id", food != null ? food.Name : string.Empty,
            "currency_type", currency.ToString().ToLowerInvariant(),
            "price", price,
            "stock_before", stockBefore,
            "stock_after", stockAfter,
            "source", "food_shop",
            "result", result,
            "failure_reason", failureReason));
    }

    public Food GetFirstFood()
    {
        return _foodList != null && _foodList.Count > 0 ? _foodList[0] : null;
    }

    public int GetStock(Food food)
    {
        return food != null && _foodAmount != null && _foodAmount.TryGetValue(food, out int amount) ? amount : 0;
    }

    public void EnsureTutorialFoodAvailable(Food food)
    {
        if (food == null || _foodAmount == null || !_foodAmount.ContainsKey(food))
            return;
        if (_foodAmount[food] <= 0)
            _foodAmount[food] = 1;
        _ui?.UpdateUI(_foodAmount);
    }

    private void TryBuyResupply()
    {
        if (G.Currency.RemoveCurrency(CurrencyType.Gems, _priceResupply))
        {
            Resupply();
        }
    }
}

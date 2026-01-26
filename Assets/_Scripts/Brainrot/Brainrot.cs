using System;
using System.Collections;
using UnityEngine;

[Serializable]
public struct BrainrotTypeData
{
    //public GameObject Model;

    public double StartIncome;
    public float MinWeight;
    public float MaxWeightMult;

    public double StartSellPrice;
}


[Serializable]
public struct BrainrotDinamicData
{
    public ElementType ElementType;
    public float WeightMultiplier;
    public double ResultIncome;
}
public class Brainrot : InventoryItem
{
    [SerializeField] private Transform _modelPoint;
    [SerializeField] private AudioSource _audio;

    [SerializeField] private BrainrotTypeData _data;
    public BrainrotTypeData Data => _data;

    private BrainrotDinamicData _dinamicData;
    public BrainrotDinamicData DinamicData => _dinamicData;

    public GameObject Model => _modelPoint.gameObject;
    public double CurrentIncome => _currentIncome;
    public long LastIncomeCollectTime => _lastIncomeTime;

    private BrainrotInfoUI _canvas;
    private GameObject _model;

    private double _currentIncome;
    private long _lastIncomeTime;

    private FieldCell _floorListener;

    private BrainrotItem _item;

    public Action Selled;
    public Action Stealed;
    private Coroutine _incomeCorutine;


    public void Init(BrainrotDinamicData rarity, FieldCell floor, long lastCollectTimestamp = -1) //передавать плейс из яйца
    {
        // rarity считается в яйце? 
        _canvas = GetComponentInChildren<BrainrotInfoUI>();
        _dinamicData = rarity;
        //_model = Instantiate(_data.,_modelPoint);
        Vector3 scale = _canvas.transform.localScale;
        _canvas.transform.parent = _modelPoint.transform;
        SetSize();
        _canvas.transform.parent = transform;
        _canvas.transform.localScale = scale;

        _dinamicData.ResultIncome = Math.Round(_data.StartIncome * G.Elements.GetMultiplaer(_dinamicData.ElementType) * (_dinamicData.WeightMultiplier / 2));
        long incomeAccumulationTime;
        if (lastCollectTimestamp == -1)
        {
            _lastIncomeTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            incomeAccumulationTime = 0;
        }
        else
        {
            _lastIncomeTime = lastCollectTimestamp;
            incomeAccumulationTime = (long)(DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(lastCollectTimestamp)).TotalSeconds;
        }
        _currentIncome = Math.Round(incomeAccumulationTime * _dinamicData.ResultIncome);

        NewPlace(floor);
        _canvas.SetInfo(_data, _dinamicData);
        SetTypeVisual();
        //_floorListener._hitEvent.AddListener(PlayerInPlace);
    }

    private void SetTypeVisual()
    {
        Renderer[] renderer = _modelPoint.GetComponentsInChildren<Renderer>();
        foreach (Renderer item in renderer)
        {
            switch (_dinamicData.ElementType)
            {
                case ElementType.Gold:
                    item.material.color = Color.yellow;
                    break;
                case ElementType.Diamond:
                    item.material.color = Color.blue;
                    break;
                case ElementType.Electric:
                    item.material.color = Color.magenta;
                    break;
                case ElementType.Fire:
                    item.material.color = Color.red;
                    break;
                default:
                    break;
            }
        }


    }
    public void NewPlace(FieldCell floor)
    {
        _floorListener = floor;
        _incomeCorutine = StartCoroutine(ProduceIncome());
        floor.PlayerEnter.AddListener(PlayerInPlace);
        floor.TakeBrainrot.AddListener(TakeBrainrot);
    }
    private void SetSize()
    {
        var m = Mathf.Max(1f, _dinamicData.WeightMultiplier); // �� ������ ������ �� ������ 1
        float scale = 1f + (m - 1f) * 0.25f;
        _modelPoint.transform.localScale = Vector3.one * scale;
    }

    private void PlayerInPlace()
    {
        GetIncome();
    }
    private void TakeBrainrot()
    {
        _floorListener.PlayerEnter.RemoveListener(PlayerInPlace);
        _floorListener.TakeBrainrot.RemoveListener(TakeBrainrot);
        _floorListener.UpdateFieldItem(Item.Free);
        StopCoroutine(_incomeCorutine);
        _incomeCorutine = null;
        G.Inventory.Add(this);
        //TestBackpackBrainrot.Instance.TakeBrainrot(this);
    }
    private void GetIncome()
    {
        G.Income.AddCoins(_currentIncome);
        //G.Currency.AddCurrency(CurrencyType.Coins, _currentIncome);
        _currentIncome = 0;
        _lastIncomeTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _floorListener.SaveData();
        _canvas.UpdateIncome(_currentIncome);
        if (_audio)
            _audio.Play();
    }

    private IEnumerator ProduceIncome()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1);
            _currentIncome += _dinamicData.ResultIncome; //2 is the magic number
            _currentIncome = (double.IsInfinity(_currentIncome)) ? float.MaxValue : _currentIncome;
            _currentIncome = Math.Round(_currentIncome);
            _canvas.UpdateIncome(_currentIncome);
        }
    }
}

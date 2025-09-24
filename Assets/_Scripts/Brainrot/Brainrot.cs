using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public struct BrainrotDinamicData
{
    public ElementType Type;
    public float WeightMultiplier;
}
public class Brainrot : MonoBehaviour
{
    [SerializeField] private Transform _modelPoint;
    [SerializeField] private AudioSource _audio;

    private BrainrotData _data;
    public BrainrotData Data => _data;

    private BrainrotDinamicData _dinamicData;
    public BrainrotDinamicData DinamicData => _dinamicData;

    private BrainrotInfoUI _canvas;
    private GameObject _model;

    private float _currentIncome;

    private FieldCell _floorListener;

    public Action Selled;
    public Action Stealed;
    private Coroutine _incomeCorutine;

    public void Init(BrainrotData data, BrainrotDinamicData rarity, FieldCell floor) //передавать плейс из €йца
    {
        // rarity считаетс€ в €йце? 
        _canvas = GetComponentInChildren<BrainrotInfoUI>();
        _data = data;
        _dinamicData = rarity;
        _canvas.SetInfo(data, rarity);
        _model = Instantiate(_data.Model,_modelPoint);
        SetSize();
        _currentIncome = 0;
        NewPlace(floor);
        SetTypeVisual();
        //_floorListener._hitEvent.AddListener(PlayerInPlace);
    }

    private void SetTypeVisual()
    {
        Renderer[] renderer = _model.GetComponentsInChildren<Renderer>();
        foreach (Renderer item in renderer)
        {
            switch (_dinamicData.Type)
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
        _model.transform.localScale = Vector3.one * (1 + ((1-_dinamicData.WeightMultiplier)/4));
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
        TestBackpackBrainrot.Instance.TakeBrainrot(this);
    }
    private void GetIncome()
    {
        CurrencyManager.Instance.AddCurrency(CurrencyType.Coins, _currentIncome);
        _currentIncome = 0;
        _canvas.UpdateIncome(_currentIncome);
        if (_audio)
            _audio.Play();
    }

    private IEnumerator ProduceIncome()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1);
            _currentIncome += _data.Income * ElementTypeMultiplaer.Init.GetMultiplaer(_dinamicData.Type) * (_dinamicData.WeightMultiplier/2); //2 is the magic number
            _currentIncome = Mathf.RoundToInt(_currentIncome);
            _canvas.UpdateIncome(_currentIncome);
        }
    }
}

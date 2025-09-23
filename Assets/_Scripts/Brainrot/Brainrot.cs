using MirraGames.SDK.Common;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;


public class Brainrot : MonoBehaviour
{
    [SerializeField] private Transform _modelPoint;
    [SerializeField] private AudioSource _audio;

    private BrainrotData _data;
    public BrainrotData Data => _data;

    private Rarity _rarity;
    public Rarity Rarity => _rarity;

    private BrainrotInfoUI _canvas;
    private GameObject _model;

    private float _currentIncome;

    [SerializeField] private InteractionRaycastListener _floorListener;

    public Action Selled;
    public Action Stealed;

    public void Init(BrainrotData data, Rarity rarity, InteractionRaycastListener floor) //передавать плейс из €йца
    {
        // rarity считаетс€ в €йце? 
        _canvas = GetComponentInChildren<BrainrotInfoUI>();
        _floorListener = floor;
        _data = data;
        _rarity = rarity;
        _canvas.SetInfo(data, rarity);
        _model = Instantiate(_data.Model,_modelPoint);
        SetSize();
        _currentIncome = 0;
        StartCoroutine(ProduceIncome());
        _floorListener._hitEvent.AddListener(PlayerInPlace);
    }
    private void SetSize()
    {
        _model.transform.localScale = Vector3.one * (1 + ((1-_rarity.WeightMultiplier)/4));
    }

    private void PlayerInPlace()
    {
        GetIncome();
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
            _currentIncome += _data.Income * _rarity.IncomeMultiplier * (_rarity.WeightMultiplier/2); //2 is the magic number
            _canvas.UpdateIncome(_currentIncome);
        }
    }
}

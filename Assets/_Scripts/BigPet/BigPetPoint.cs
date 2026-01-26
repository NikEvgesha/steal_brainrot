using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BigPetPoint : MonoBehaviour
{
    [SerializeField] private List<Brainrot> _pets;
    [SerializeField] private GameObject _feedButton;
    [SerializeField] private Transform _foodPoint;
    [SerializeField] private Transform _petPoint;
    [SerializeField] private int _baseXPperLvl;
    [SerializeField] private int _xpAddintPerLvl;
    [SerializeField] private Slider _xpProgressBar;
    [SerializeField] private Slider _foodTimeBar;
    [SerializeField] private Text _xpProgressText;
    [SerializeField] private Text _foodTimeBarText;
    [SerializeField] private float _foodScaler;
    [SerializeField] private float _petScaler;
    [SerializeField] private int _lvlsPerPet = 5; // через сколько уровней открывается новый пет
    [SerializeField] private BrainrotInfoUI _petInfoUI;
    [SerializeField] private AudioSource _audio;

    private bool _playerInArea;
    private int _currentXp;
    private int _currentLvl;
    private int _xpForNextLvl;
    private bool _feeding;
    private Food _currentFood;
    private GameObject _currentPet;
    private int _currentPetIdx;
    private int _maxAvailablePetIdx;
    private int _maxLvl;
    private double _currentIncome;
    private double _accumulatedIncome;
    private BigPetSetUI _setPetUI;
    private DateTime _lastIncomeCollectTimestamp;

    [HideInInspector] public UnityEvent PlayerEnter;
    [HideInInspector] public UnityEvent PlayerExit;

    private void Start()
    {
        // TODO load current xp
        // load current pet
        // load food
        // set xp slider
        // set pet scale
        // load income
        _setPetUI = GetComponentInChildren<BigPetSetUI>();
        _setPetUI.PetSlotClicked.AddListener(ChangeActivePet);
        _setPetUI.InitUI(_pets);
        _currentLvl = G.Save.LoadBigPetLvl();
        _currentXp = G.Save.LoadBigPetXP(); ;
        _xpForNextLvl = _baseXPperLvl + _xpAddintPerLvl * (_currentLvl - 1);
        _currentPetIdx = G.Save.LoadBigPetId();
        _maxAvailablePetIdx = _currentLvl / _lvlsPerPet;
        _maxAvailablePetIdx = _maxAvailablePetIdx > (_pets.Count - 1) ? _pets.Count - 1 : _maxAvailablePetIdx;
        _maxLvl = _pets.Count * _lvlsPerPet;
        _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);
        SetPet(_currentPetIdx);
        CheckLvl();
        _currentIncome = _pets[_maxAvailablePetIdx].Data.StartIncome; // TODO;
        _petInfoUI.SetInfo(_currentIncome);
        _foodTimeBar.gameObject.SetActive(false);

        long incomeAccumulateTime;
        string timestamp = G.Save.LoadBigPetIncomeTime();
        if (timestamp.Length == 0)
        {
            incomeAccumulateTime = 0;
            _lastIncomeCollectTimestamp = DateTime.UtcNow;
            G.Save.SaveBigPetIncomeTime(_lastIncomeCollectTimestamp.ToString());
        } else
        {
            _lastIncomeCollectTimestamp = DateTime.Parse(timestamp);
            incomeAccumulateTime = (long)(DateTime.UtcNow - _lastIncomeCollectTimestamp).TotalSeconds;
        }  
        _accumulatedIncome = incomeAccumulateTime * _currentIncome;
        StartCoroutine(ProduceIncome());
        
    }

    public void _OnPlayerEnter()
    {
        G.QuickAccess.SwitchActiveItem.AddListener(CheckPlayer);
        _playerInArea = true;
        CheckPlayer(G.QuickAccess.CurrentActive);
        PlayerEnter?.Invoke();
        GetIncome();
    }

    public void _OnPlayerExit() {
        G.QuickAccess.SwitchActiveItem.RemoveListener(CheckPlayer);
        _feedButton.SetActive(false);
        _playerInArea = false;
        PlayerExit?.Invoke();
    }


    private void CheckPlayer(InventoryItem item = null)
    {
        if (_feeding || !_playerInArea) return;
        _feedButton.SetActive(item != null && item.Type == Item.Food);
    }

    public void _Feed()
    {
        if (_feeding) return;

        InventoryItem currentItem = G.QuickAccess.CurrentActive;
        Food food = null;
        bool isFood = ((currentItem.Type == Item.Food) && currentItem.TryGetComponent<Food>(out food));
        if (!isFood) return;

        G.QuickAccess.DropCurrent(_foodPoint);
        _currentFood = food;
        _currentFood.transform.localScale = Vector3.one * _foodScaler;
        _feeding = true;
        _feedButton.SetActive(false);
        StartCoroutine(FeedProcess());
    }

    private IEnumerator FeedProcess()
    {
        _foodTimeBar.gameObject.SetActive(true);
        int secondsRemains = _currentFood.Data.SecondsDuration;
        _foodTimeBarText.text = String.Format(
                    "{0}:{1}",
                    (secondsRemains / 60).ToString("D2"),
                    (secondsRemains % 60).ToString("D2")
                );
        _foodTimeBar.value = 1;
        while (secondsRemains > 0)
        {
            yield return new WaitForSeconds(1);
            secondsRemains--;
            _currentXp += _currentFood.Data.XPPerSecond;
            CheckLvl();
            G.Save.SaveBigPetXP(_currentXp);
            float t = (float)secondsRemains / _currentFood.Data.SecondsDuration;
            _currentFood.transform.localScale = Vector3.Lerp(Vector3.one * _foodScaler, Vector3.one, 1 - t);
            _foodTimeBar.value = t;
            _foodTimeBarText.text = String.Format(
                    "{0}:{1}",
                    (secondsRemains / 60).ToString("D2"),
                    (secondsRemains % 60).ToString("D2")
                ); ;
        }
        _feeding = false;
        Destroy(_currentFood.gameObject);
        _currentFood = null;
        CheckPlayer(G.QuickAccess.CurrentActive);
        _foodTimeBar.gameObject.SetActive(false);
    }

    private void CheckLvl()
    {
        //if (_currentLvl >= _maxLvl) return;

        if (_currentXp >= _xpForNextLvl)
        {
            _currentLvl++;
            G.Save.SaveBigPetLvl(_currentLvl);
            _currentXp -= _xpForNextLvl;
            _xpForNextLvl += _xpAddintPerLvl;
            if (_currentLvl % _lvlsPerPet == 1 && (_maxAvailablePetIdx < _pets.Count - 1))
            {
                _maxAvailablePetIdx++;
                _currentIncome = _pets[_maxAvailablePetIdx].Data.StartIncome;
                _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);
                _petInfoUI.SetInfo(_currentIncome);
                SetPet(_maxAvailablePetIdx);
            }

            CheckScale();

        }

        _xpProgressBar.value = (float)_currentXp / _xpForNextLvl;
        _xpProgressText.text = string.Format("LVL {0} : {1} / {2}", _currentLvl, _currentXp, _xpForNextLvl);
    }


    private void CheckScale()
    {
        if (_currentPetIdx < _maxAvailablePetIdx || _currentLvl >= _maxLvl)
        {
            _currentPet.transform.localScale = _petScaler * Vector3.one;
        }
        else
        {
            float t = (((_currentLvl - 1) % _lvlsPerPet) + 1) / (float)_lvlsPerPet;
            _currentPet.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * _petScaler, t);
        }
    }


    private void SetPet(int idx)
    {
        if (_currentPet != null)
        {
            Destroy(_currentPet.gameObject);
        }
        _currentPetIdx = idx;
        G.Save.SaveBigPetId(_currentPetIdx);
        _currentPet = Instantiate(_pets[idx].Model, _petPoint);
        _setPetUI.ChangeActivePet(_pets[idx]);
        CheckScale();

    }

    private void ChangeActivePet(Brainrot pet)
    {
        int idx = _pets.IndexOf(pet);

        if (idx < 0) return;

        SetPet(idx);

    }

    private void GetIncome()
    {
        G.Income.AddCoins(_accumulatedIncome);
        _accumulatedIncome = 0;
        _petInfoUI.UpdateIncome(_accumulatedIncome);
        _lastIncomeCollectTimestamp = DateTime.UtcNow;
        G.Save.SaveBigPetIncomeTime(_lastIncomeCollectTimestamp.ToString());
        if (_audio)
            _audio.Play();
    }

    private IEnumerator ProduceIncome()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1);
            _accumulatedIncome +=  _currentIncome; // TODO: Income math
            _accumulatedIncome = (double.IsInfinity(_accumulatedIncome)) ? float.MaxValue : _accumulatedIncome;
            _accumulatedIncome = Math.Round(_accumulatedIncome);
            _petInfoUI.UpdateIncome(_accumulatedIncome);
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        _petInfoUI.gameObject.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        _petInfoUI.gameObject.SetActive(false);
    }

}

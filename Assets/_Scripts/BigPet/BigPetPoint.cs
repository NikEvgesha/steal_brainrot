using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BigPetPoint : MonoBehaviour
{
    [SerializeField] private bool _remoteMode;
    [SerializeField] private double _unlockPrice;
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
    [SerializeField] private int _lvlsPerPet = 5;
    [SerializeField] private BrainrotInfoUI _petInfoUI;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private InteractionPanel _buyPanel;
    [SerializeField] private GameObject _changePetArea;

    private bool _purchased;
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
    private bool _initializedLocal;
    private Coroutine _incomeRoutine;
    private bool _quickAccessBound;

    public double CurrentIncomePerSecond => _purchased ? _currentIncome : 0d;

    [HideInInspector] public UnityEvent PlayerEnter;
    [HideInInspector] public UnityEvent PlayerExit;

    private void Start()
    {
        if (_remoteMode) return;
        InitLocal();
    }

    private void InitLocal()
    {
        if (_initializedLocal) return;
        if (_remoteMode) return;
        _initializedLocal = true;

        _setPetUI = GetComponentInChildren<BigPetSetUI>();
        if (_setPetUI == null)
        {
            Debug.LogWarning("[BigPetPoint] BigPetSetUI not found.");
            return;
        }

        _setPetUI.SetRemoteMode(false);
        if (_pets == null || _pets.Count == 0)
        {
            Debug.LogWarning("[BigPetPoint] Pets list is empty.");
            return;
        }

        _setPetUI.PetSlotClicked.AddListener(ChangeActivePet);
        _setPetUI.InitUI(_pets);

        CacheSceneRefs();

        _purchased = ResolvePurchaseState();
        if (_purchased)
            InitPurchasedState();
        else
            PrepareLockedState();
    }

    public void _OnPlayerEnter()
    {
        if (_remoteMode) return;

        _playerInArea = true;
        if (_purchased)
        {
            SetQuickAccessBinding(true);
            CheckPlayer(G.QuickAccess != null ? G.QuickAccess.CurrentActive : null);
            GetIncome();
            if (_petInfoUI != null)
                _petInfoUI.gameObject.SetActive(true);
        }
        else
        {
            if (_buyPanel != null)
                _buyPanel.gameObject.SetActive(true);
        }

        PlayerEnter?.Invoke();
    }

    public void _OnPlayerExit()
    {
        if (_remoteMode) return;

        if (_purchased)
        {
            SetQuickAccessBinding(false);
            if (_feedButton != null)
                _feedButton.SetActive(false);
            if (_petInfoUI != null)
                _petInfoUI.gameObject.SetActive(true);
        }
        else
        {
            if (_buyPanel != null)
                _buyPanel.gameObject.SetActive(false);
        }

        _playerInArea = false;
        PlayerExit?.Invoke();
    }

    private void HideRemoteUI()
    {
        if (_feedButton != null) _feedButton.SetActive(false);
        if (_foodTimeBar != null) _foodTimeBar.gameObject.SetActive(false);
        if (_xpProgressBar != null) _xpProgressBar.gameObject.SetActive(false);
        if (_foodTimeBarText != null) _foodTimeBarText.gameObject.SetActive(false);
        if (_xpProgressText != null) _xpProgressText.gameObject.SetActive(false);
        if (_setPetUI != null) _setPetUI.gameObject.SetActive(false);
        if (_petInfoUI != null) _petInfoUI.gameObject.SetActive(false);
        if (_buyPanel != null) _buyPanel.gameObject.SetActive(false);
        if (_changePetArea != null) _changePetArea.SetActive(false);
    }

    private void ShowLocalUI()
    {
        if (_feedButton != null) _feedButton.SetActive(false);
        if (_foodTimeBar != null) _foodTimeBar.gameObject.SetActive(false);
        if (_xpProgressBar != null) _xpProgressBar.gameObject.SetActive(true);
        if (_foodTimeBarText != null) _foodTimeBarText.gameObject.SetActive(false);
        if (_xpProgressText != null) _xpProgressText.gameObject.SetActive(true);
        if (_setPetUI != null) _setPetUI.gameObject.SetActive(_purchased);
        if (_petInfoUI != null) _petInfoUI.gameObject.SetActive(_purchased);
        if (_changePetArea != null) _changePetArea.SetActive(_purchased);
        if (_buyPanel != null) _buyPanel.gameObject.SetActive(false);
    }

    private void CheckPlayer(InventoryItem item = null)
    {
        if (_remoteMode) return;
        if (_feeding || !_playerInArea) return;
        if (_feedButton != null)
            _feedButton.SetActive(item != null && item.Type == Item.Food);
    }

    public void _Feed()
    {
        if (_remoteMode) return;
        if (_feeding) return;

        InventoryItem currentItem = G.QuickAccess.CurrentActive;
        if (currentItem == null) return;

        Food food = null;
        bool isFood = currentItem.Type == Item.Food && currentItem.TryGetComponent(out food);
        if (!isFood) return;

        G.QuickAccess.DropCurrent(_foodPoint);
        _currentFood = food;
        _currentFood.transform.localScale = Vector3.one * _foodScaler;
        _feeding = true;
        if (_feedButton != null)
            _feedButton.SetActive(false);
        StartCoroutine(FeedProcess());
    }

    private IEnumerator FeedProcess()
    {
        if (_remoteMode) yield break;
        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(true);

        int secondsRemains = _currentFood.Data.SecondsDuration;
        if (_foodTimeBarText != null)
        {
            _foodTimeBarText.text = string.Format(
                "{0}:{1}",
                (secondsRemains / 60).ToString("D2"),
                (secondsRemains % 60).ToString("D2"));
        }

        if (_foodTimeBar != null)
            _foodTimeBar.value = 1f;

        while (secondsRemains > 0)
        {
            yield return new WaitForSeconds(1f);
            secondsRemains--;
            _currentXp += _currentFood.Data.XPPerSecond;
            CheckLvl();
            G.Save.SaveBigPetXP(_currentXp);

            float t = (float)secondsRemains / _currentFood.Data.SecondsDuration;
            _currentFood.transform.localScale = Vector3.Lerp(Vector3.one * _foodScaler, Vector3.one, 1f - t);
            if (_foodTimeBar != null)
                _foodTimeBar.value = t;
            if (_foodTimeBarText != null)
            {
                _foodTimeBarText.text = string.Format(
                    "{0}:{1}",
                    (secondsRemains / 60).ToString("D2"),
                    (secondsRemains % 60).ToString("D2"));
            }
        }

        _feeding = false;
        Destroy(_currentFood.gameObject);
        _currentFood = null;
        CheckPlayer(G.QuickAccess != null ? G.QuickAccess.CurrentActive : null);
        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);
    }

    private void CheckLvl()
    {
        if (_remoteMode) return;

        if (_currentXp >= _xpForNextLvl)
        {
           while (_currentXp >= _xpForNextLvl)
            {
                _currentLvl++;
                _currentXp -= _xpForNextLvl;
                _xpForNextLvl += _xpAddintPerLvl;
            }       
            G.Save.SaveBigPetLvl(_currentLvl);
            BaseDirtyTracker.MarkDirty();

            if (_currentLvl % _lvlsPerPet == 1 && _maxAvailablePetIdx < _pets.Count - 1)
            {
                _maxAvailablePetIdx++;
                _currentIncome = _pets[_maxAvailablePetIdx].Data.StartIncome;
                if (_setPetUI != null)
                    _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);
                if (_petInfoUI != null)
                    _petInfoUI.SetInfo(_currentIncome);
                SetPet(_maxAvailablePetIdx);
            }

            CheckScale();
        }

        if (_xpProgressBar != null)
            _xpProgressBar.value = (float)_currentXp / _xpForNextLvl;
        if (_xpProgressText != null)
            _xpProgressText.text = string.Format("LVL {0} : {1} / {2}", _currentLvl, _currentXp, _xpForNextLvl);
    }

    private void CheckScale()
    {
        if (_currentPet == null)
            return;

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
        if (_pets == null || _pets.Count == 0)
            return;

        idx = Mathf.Clamp(idx, 0, _pets.Count - 1);

        if (_currentPet != null)
            Destroy(_currentPet.gameObject);

        _currentPetIdx = idx;
        if (!_remoteMode)
            G.Save.SaveBigPetId(_currentPetIdx);
        if (!_remoteMode)
            BaseDirtyTracker.MarkDirty();

        if (_petPoint != null)
            _currentPet = Instantiate(_pets[idx].Model, _petPoint);

        if (_setPetUI != null)
            _setPetUI.ChangeActivePet(_pets[idx]);

        CheckScale();
    }

    private void ChangeActivePet(Brainrot pet)
    {
        if (_remoteMode) return;
        int idx = _pets.IndexOf(pet);
        if (idx < 0) return;
        SetPet(idx);
    }

    private void GetIncome()
    {
        if (_remoteMode) return;
        if (!_purchased) return;

        G.Income.AddCoins(_accumulatedIncome);
        _accumulatedIncome = 0;
        if (_petInfoUI != null)
            _petInfoUI.UpdateIncome(_accumulatedIncome);

        _lastIncomeCollectTimestamp = DateTime.UtcNow;
        G.Save.SaveBigPetIncomeTime(_lastIncomeCollectTimestamp.ToString());
        if (_audio)
            _audio.Play();
    }

    private IEnumerator ProduceIncome()
    {
        if (_remoteMode) yield break;

        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (!_purchased)
                continue;

            _accumulatedIncome += _currentIncome;
            _accumulatedIncome = double.IsInfinity(_accumulatedIncome) ? float.MaxValue : _accumulatedIncome;
            _accumulatedIncome = Math.Round(_accumulatedIncome);
            if (_petInfoUI != null)
                _petInfoUI.UpdateIncome(_accumulatedIncome);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_remoteMode || !_purchased) return;
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_remoteMode || !_purchased) return;
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(true);
    }

    public void ApplyRemoteState(int petId, int lvl, int xp, bool purchased = true)
    {
        _remoteMode = true;
        StopIncomeRoutine();
        SetQuickAccessBinding(false);
        HideRemoteUI();
        CacheSceneRefs();

        _purchased = purchased;
        if (!_purchased)
        {
            if (_currentPet != null)
            {
                Destroy(_currentPet.gameObject);
                _currentPet = null;
            }
            _currentIncome = 0d;
            _accumulatedIncome = 0d;
            return;
        }

        _currentLvl = Mathf.Max(1, lvl);
        _currentXp = Mathf.Max(0, xp);
        _xpForNextLvl = _baseXPperLvl + _xpAddintPerLvl * (_currentLvl - 1);

        _maxAvailablePetIdx = _currentLvl / _lvlsPerPet;
        _maxAvailablePetIdx = _maxAvailablePetIdx > (_pets.Count - 1) ? _pets.Count - 1 : _maxAvailablePetIdx;
        _maxLvl = _pets.Count * _lvlsPerPet;

        if (_setPetUI != null)
            _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);

        petId = Mathf.Clamp(petId, 0, _pets.Count - 1);
        SetPet(petId);

        if (_petInfoUI != null)
        {
            _currentIncome = _pets[_maxAvailablePetIdx].Data.StartIncome;
            _petInfoUI.SetInfo(_currentIncome);
            _petInfoUI.UpdateIncome(0);
        }

        if (_feedButton != null) _feedButton.SetActive(false);
        if (_foodTimeBar != null) _foodTimeBar.gameObject.SetActive(false);
    }

    public void ApplyRemoteDefaultState(int petId = 0, int lvl = 1, int xp = 0, bool purchased = false)
    {
        _remoteMode = true;
        HideRemoteUI();

        if (_pets == null || _pets.Count == 0)
            return;

        ApplyRemoteState(petId, lvl, xp, purchased);
    }

    public void SetRemoteMode(bool remote)
    {
        _remoteMode = remote;
        if (_setPetUI == null)
            _setPetUI = GetComponentInChildren<BigPetSetUI>(true);
        if (_setPetUI != null)
            _setPetUI.SetRemoteMode(remote);

        if (_remoteMode)
        {
            StopIncomeRoutine();
            SetQuickAccessBinding(false);
            HideRemoteUI();
        }
        else
        {
            InitLocal();
            if (_purchased)
                ShowLocalUI();
            else
                PrepareLockedState();
        }
    }

    public void _TryBuy()
    {
        if (_remoteMode || _purchased)
            return;

        if (!G.Currency.RemoveCurrency(CurrencyType.Coins, _unlockPrice))
            return;

        _purchased = true;
        G.Save.SaveBigPetStatus(true);
        InitPurchasedState();
        BaseDirtyTracker.MarkDirty();

        if (_playerInArea)
        {
            SetQuickAccessBinding(true);
            CheckPlayer(G.QuickAccess != null ? G.QuickAccess.CurrentActive : null);
        }
    }

    private void CacheSceneRefs()
    {
        if (_changePetArea == null)
        {
            var area = transform.Find("ChangePetArea");
            if (area != null)
                _changePetArea = area.gameObject;
        }
    }

    private bool ResolvePurchaseState()
    {
        var purchased = G.Save.LoadBigPetStatus();
        if (purchased)
            return true;

        // Backward compatibility for saves created before purchase gating.
        var legacyLevel = G.Save.LoadBigPetLvl();
        var legacyXp = G.Save.LoadBigPetXP();
        var legacyPetId = G.Save.LoadBigPetId();
        if (legacyLevel > 1 || legacyXp > 0 || legacyPetId > 0)
        {
            purchased = true;
            G.Save.SaveBigPetStatus(true);
        }

        return purchased;
    }

    private void InitPurchasedState()
    {
        if (_pets == null || _pets.Count == 0)
            return;

        _currentLvl = Mathf.Max(1, G.Save.LoadBigPetLvl());
        _currentXp = Mathf.Max(0, G.Save.LoadBigPetXP());
        _xpForNextLvl = _baseXPperLvl + _xpAddintPerLvl * (_currentLvl - 1);
        _currentPetIdx = Mathf.Clamp(G.Save.LoadBigPetId(), 0, _pets.Count - 1);
        _maxAvailablePetIdx = Mathf.Clamp(_currentLvl / _lvlsPerPet, 0, _pets.Count - 1);
        _maxLvl = _pets.Count * _lvlsPerPet;

        if (_setPetUI != null)
        {
            _setPetUI.gameObject.SetActive(true);
            _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);
        }

        if (_changePetArea != null)
            _changePetArea.SetActive(true);

        SetPet(_currentPetIdx);
        CheckLvl();

        _currentIncome = _pets[_maxAvailablePetIdx].Data.StartIncome;
        if (_petInfoUI != null)
        {
            _petInfoUI.SetInfo(_currentIncome);
            _petInfoUI.gameObject.SetActive(true);
        }

        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);

        if (_buyPanel != null)
            _buyPanel.gameObject.SetActive(false);

        long incomeAccumulateTime;
        string timestamp = G.Save.LoadBigPetIncomeTime();
        if (string.IsNullOrEmpty(timestamp))
        {
            incomeAccumulateTime = 0;
            _lastIncomeCollectTimestamp = DateTime.UtcNow;
            G.Save.SaveBigPetIncomeTime(_lastIncomeCollectTimestamp.ToString());
        }
        else
        {
            _lastIncomeCollectTimestamp = DateTime.Parse(timestamp);
            incomeAccumulateTime = (long)(DateTime.UtcNow - _lastIncomeCollectTimestamp).TotalSeconds;
        }

        _accumulatedIncome = Math.Max(0d, incomeAccumulateTime * _currentIncome);
        if (_petInfoUI != null)
            _petInfoUI.UpdateIncome(_accumulatedIncome);

        EnsureIncomeRoutine();
    }

    private void PrepareLockedState()
    {
        StopIncomeRoutine();
        SetQuickAccessBinding(false);
        _currentIncome = 0d;
        _accumulatedIncome = 0d;

        if (_currentPet != null)
        {
            Destroy(_currentPet.gameObject);
            _currentPet = null;
        }

        if (_feedButton != null)
            _feedButton.SetActive(false);
        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);

        if (_setPetUI != null)
        {
            _setPetUI.OpenUI(false);
            _setPetUI.gameObject.SetActive(false);
        }

        if (_petInfoUI != null)
        {
            _petInfoUI.UpdateIncome(0d);
            _petInfoUI.gameObject.SetActive(false);
        }

        if (_changePetArea != null)
            _changePetArea.SetActive(false);

        if (_buyPanel != null)
        {
            _buyPanel.SetInfo("Activate", Math.Round(_unlockPrice).ToString("0", CultureInfo.InvariantCulture));
            _buyPanel.gameObject.SetActive(false);
        }
    }

    private void EnsureIncomeRoutine()
    {
        if (_remoteMode || !_purchased || _incomeRoutine != null)
            return;

        _incomeRoutine = StartCoroutine(ProduceIncome());
    }

    private void StopIncomeRoutine()
    {
        if (_incomeRoutine == null)
            return;

        StopCoroutine(_incomeRoutine);
        _incomeRoutine = null;
    }

    private void SetQuickAccessBinding(bool enabled)
    {
        if (G.QuickAccess == null)
            return;

        if (enabled)
        {
            if (_quickAccessBound)
                return;

            G.QuickAccess.SwitchActiveItem.AddListener(CheckPlayer);
            _quickAccessBound = true;
            return;
        }

        if (!_quickAccessBound)
            return;

        G.QuickAccess.SwitchActiveItem.RemoveListener(CheckPlayer);
        _quickAccessBound = false;
    }
}


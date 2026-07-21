using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BigPetPoint : MonoBehaviour
{
    public static event Action<int> LocalLevelChanged;

    private static readonly HashSet<string> ExcludedBigPetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "balerina",
        "BalerinaCapuchina",
        "frutodrillo",
        "Frutodillo",
        "sahur",
        "Sahur",
        "TungTungSahur",
        "GlorboFruttodrillo",
    };

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
    [SerializeField] private TMP_Text _xpProgressText;
    [SerializeField] private TMP_Text _foodTimeBarText;
    [SerializeField] private float _foodScaler;
    [SerializeField] private float _petScaler;
    [SerializeField] private float _petGroundOffset;
    [SerializeField] private int _lvlsPerPet = 5;
    [SerializeField] private BrainrotInfoUI _petInfoUI;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private InteractionPanel _buyPanel;
    [SerializeField] private GameObject _changePetArea;
    [SerializeField, Min(1f)] private float _changePetInteractionDistance = 5f;

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
    private readonly List<Brainrot> _activePets = new List<Brainrot>();

    public double CurrentIncomePerSecond => _purchased ? _currentIncome : 0d;
    public double UnlockPrice => Math.Max(0d, _unlockPrice);
    public bool IsPurchased => _purchased;
    public int CurrentLevel => Mathf.Max(1, _currentLvl);
    public bool IsPlayerInArea => _playerInArea;
    public Transform BuyActionTarget => _buyPanel != null ? _buyPanel.transform : transform;
    public Transform FeedActionTarget => _feedButton != null ? _feedButton.transform : transform;
    public bool IsRemoteMode => _remoteMode;
    public bool HasCollectibleIncome => !_remoteMode && _purchased && _accumulatedIncome > 0d;

    [HideInInspector] public UnityEvent PlayerEnter;
    [HideInInspector] public UnityEvent PlayerExit;

    private void Start()
    {
        if (_remoteMode) return;
        InitLocal();
    }

    private void OnEnable()
    {
        if (_initializedLocal && !_remoteMode && _purchased)
            EnsureIncomeRoutine();
    }

    private void OnDisable()
    {
        StopIncomeRoutine();
    }

    private void OnDestroy()
    {
        if (_setPetUI != null)
            _setPetUI.PetSlotClicked.RemoveListener(ChangeActivePet);
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
            PrepareLockedState();
            return;
        }

        _setPetUI.SetRemoteMode(false);
        if (!ResolvePetList())
        {
            Debug.LogWarning("[BigPetPoint] Pets list is empty.");
            PrepareLockedState();
            return;
        }

        BindPetSelection();

        CacheSceneRefs();

        ReloadLocalStateFromSave();
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
            ApplyLockedUiState(true);
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
            ApplyLockedUiState(false);
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
        if (_feedButton == null) return;

        bool canFeed = _purchased && !_feeding && _playerInArea && item != null && item.Type == Item.Food;
        if (canFeed)
        {
            InteractionPanel feedPanel = _feedButton.GetComponent<InteractionPanel>();
            if (feedPanel != null)
                feedPanel.SetInfo(LocalizationUtils.T("Feed", "Кормить"));

            if (_buyPanel != null)
                _buyPanel.gameObject.SetActive(false);
        }

        _feedButton.SetActive(canFeed);
    }

    public void _Feed()
    {
        if (_remoteMode || !_purchased || !_playerInArea) return;
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
        TutorialSignals.Raise(TutorialSignalType.BigPetFed, this, food.Name, Item.Food);
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
            LocalLevelChanged?.Invoke(_currentLvl);
            BaseDirtyTracker.MarkDirty();

            var newMaxAvailablePetIdx = GetMaxAvailablePetIndexForLevel(_currentLvl);
            if (newMaxAvailablePetIdx != _maxAvailablePetIdx)
            {
                _maxAvailablePetIdx = newMaxAvailablePetIdx;
                _currentIncome = _activePets[_maxAvailablePetIdx].Data.StartIncome;
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
            var levelsPerPet = Mathf.Max(1, _lvlsPerPet);
            float t = (((_currentLvl - 1) % levelsPerPet) + 1) / (float)levelsPerPet;
            _currentPet.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * _petScaler, t);
        }

        AlignCurrentPetToGround();
    }

    private void SetPet(int idx)
    {
        if (_activePets == null || _activePets.Count == 0)
            return;

        idx = Mathf.Clamp(idx, 0, _activePets.Count - 1);

        if (_currentPet != null)
            Destroy(_currentPet.gameObject);

        _currentPetIdx = idx;
        if (!_remoteMode)
            G.Save.SaveBigPetId(_currentPetIdx);
        if (!_remoteMode)
            BaseDirtyTracker.MarkDirty();

        if (_petPoint != null)
        {
            _currentPet = Instantiate(_activePets[idx].Model, _petPoint, false);
            _currentPet.transform.localPosition = Vector3.zero;
        }

        if (_setPetUI != null)
            _setPetUI.ChangeActivePet(_activePets[idx]);

        CheckScale();
    }

    private void AlignCurrentPetToGround()
    {
        if (_currentPet == null)
            return;

        Renderer[] renderers = _currentPet.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        float targetBottom = (_petPoint != null ? _petPoint.position.y : transform.position.y) + _petGroundOffset;
        float deltaY = targetBottom - bounds.min.y;
        if (Mathf.Abs(deltaY) > 0.001f)
            _currentPet.transform.position += Vector3.up * deltaY;
    }

    private void ChangeActivePet(Brainrot pet)
    {
        if (_remoteMode) return;
        int idx = _activePets.IndexOf(pet);
        if (idx < 0) return;
        if (idx > _maxAvailablePetIdx) return;
        SetPet(idx);
    }

    private void GetIncome()
    {
        CollectIncome();
    }

    public double CollectIncome(bool playAudio = true)
    {
        if (_remoteMode) return 0d;
        if (!_purchased) return 0d;

        var collected = Math.Max(0d, _accumulatedIncome);
        if (collected <= 0d)
            return 0d;

        if (!TryAddCoins(collected))
            return 0d;

        _accumulatedIncome = 0;
        if (_petInfoUI != null)
            _petInfoUI.UpdateIncome(_accumulatedIncome);

        var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _lastIncomeCollectTimestamp = DateTimeOffset.FromUnixTimeSeconds(nowTs).UtcDateTime;
        G.Save.SaveBigPetIncomeTime(nowTs.ToString(CultureInfo.InvariantCulture));
        if (playAudio && _audio)
            _audio.Play();

        return collected;
    }

    private static bool TryAddCoins(double amount)
    {
        if (amount <= 0d)
            return false;

        if (G.Income != null)
            return G.Income.TryAddCoins(amount);

        if (G.Currency != null)
        {
            G.Currency.AddCurrency(CurrencyType.Coins, amount);
            return true;
        }

        Debug.LogWarning("[BigPetPoint] Cannot collect income: income and currency managers are not initialized.");
        return false;
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
        if (!other.CompareTag("Player")) return;
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_remoteMode || !_purchased) return;
        if (!other.CompareTag("Player")) return;
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(false);
    }

    public void ApplyRemoteState(int petId, int lvl, int xp, bool purchased = true)
    {
        _remoteMode = true;
        StopIncomeRoutine();
        SetQuickAccessBinding(false);
        HideRemoteUI();
        CacheSceneRefs();

        _purchased = purchased;
        if (!ResolvePetList())
        {
            _currentIncome = 0d;
            _accumulatedIncome = 0d;
            return;
        }

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

        _maxAvailablePetIdx = GetMaxAvailablePetIndexForLevel(_currentLvl);
        _maxLvl = _activePets.Count * Mathf.Max(1, _lvlsPerPet);

        if (_setPetUI != null)
            _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);

        petId = Mathf.Clamp(petId, 0, _maxAvailablePetIdx);
        SetPet(petId);

        _currentIncome = _activePets[_maxAvailablePetIdx].Data.StartIncome;
        if (_petInfoUI != null)
        {
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

        if (!ResolvePetList())
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
            if (!_initializedLocal)
            {
                InitLocal();
            }
            else
            {
                ReloadLocalStateFromSave();
            }

            if (_purchased)
                ShowLocalUI();
        }
    }

    private void ReloadLocalStateFromSave()
    {
        if (_setPetUI == null)
            _setPetUI = GetComponentInChildren<BigPetSetUI>(true);
        if (_setPetUI != null)
        {
            _setPetUI.SetRemoteMode(false);
            BindPetSelection();
        }

        CacheSceneRefs();
        _purchased = ResolvePurchaseState();
        if (_purchased)
            InitPurchasedState();
        else
            PrepareLockedState();
    }

    private void BindPetSelection()
    {
        if (_setPetUI == null)
            return;

        _setPetUI.PetSlotClicked.RemoveListener(ChangeActivePet);
        _setPetUI.PetSlotClicked.AddListener(ChangeActivePet);
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
        LocalLevelChanged?.Invoke(CurrentLevel);
        BaseDirtyTracker.MarkDirty();
        TutorialSignals.Raise(TutorialSignalType.BigPetPurchased, this, value: _unlockPrice);

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

        if (_changePetArea == null)
            return;

        InteractionRaycastListener listener = _changePetArea.GetComponent<InteractionRaycastListener>();
        if (listener != null)
            listener.MaxDistance = Mathf.Max(listener.MaxDistance, _changePetInteractionDistance);

        BoxCollider interactionCollider = _changePetArea.GetComponent<BoxCollider>();
        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true;
            Vector3 size = interactionCollider.size;
            interactionCollider.size = new Vector3(
                Mathf.Max(4.5f, size.x),
                Mathf.Max(2f, size.y),
                Mathf.Max(4.5f, size.z));
            Vector3 center = interactionCollider.center;
            interactionCollider.center = new Vector3(center.x, Mathf.Max(0.9f, center.y), center.z);
        }

        if (_setPetUI != null)
            _setPetUI.ConfigureWorldInteraction(interactionCollider, _changePetInteractionDistance);
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
        if (!ResolvePetList())
            return;

        _currentLvl = Mathf.Max(1, G.Save.LoadBigPetLvl());
        _currentXp = Mathf.Max(0, G.Save.LoadBigPetXP());
        _xpForNextLvl = _baseXPperLvl + _xpAddintPerLvl * (_currentLvl - 1);
        _maxAvailablePetIdx = GetMaxAvailablePetIndexForLevel(_currentLvl);
        _currentPetIdx = Mathf.Clamp(G.Save.LoadBigPetId(), 0, _maxAvailablePetIdx);
        _maxLvl = _activePets.Count * Mathf.Max(1, _lvlsPerPet);

        if (_setPetUI != null)
        {
            _setPetUI.gameObject.SetActive(true);
            _setPetUI.InitUI(_activePets);
            _setPetUI.SetMaxAvailablePet(_maxAvailablePetIdx);
        }

        if (_changePetArea != null)
            _changePetArea.SetActive(true);

        if (_xpProgressBar != null)
            _xpProgressBar.gameObject.SetActive(true);
        if (_xpProgressText != null)
            _xpProgressText.gameObject.SetActive(true);

        SetPet(_currentPetIdx);
        CheckLvl();

        _currentIncome = _activePets[_maxAvailablePetIdx].Data.StartIncome;
        if (_petInfoUI != null)
        {
            _petInfoUI.SetInfo(_currentIncome);
            _petInfoUI.gameObject.SetActive(true);
        }

        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);
        if (_foodTimeBarText != null)
            _foodTimeBarText.gameObject.SetActive(false);

        if (_buyPanel != null)
            _buyPanel.gameObject.SetActive(false);

        long incomeAccumulateTime;
        string timestamp = G.Save.LoadBigPetIncomeTime();
        var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!TryParseIncomeTimestamp(timestamp, out var lastCollectTs))
        {
            lastCollectTs = nowTs;
            G.Save.SaveBigPetIncomeTime(lastCollectTs.ToString(CultureInfo.InvariantCulture));
        }

        if (lastCollectTs > nowTs)
            lastCollectTs = nowTs;

        _lastIncomeCollectTimestamp = DateTimeOffset.FromUnixTimeSeconds(lastCollectTs).UtcDateTime;
        incomeAccumulateTime = Math.Max(0L, nowTs - lastCollectTs);

        _accumulatedIncome = Math.Max(0d, incomeAccumulateTime * _currentIncome);
        if (_petInfoUI != null)
        {
            _petInfoUI.UpdateIncome(_accumulatedIncome);
            if (incomeAccumulateTime > 0)
                _petInfoUI.UpdateOfflineIncome(_accumulatedIncome);
        }

        EnsureIncomeRoutine();
    }

    private bool ResolvePetList()
    {
        _activePets.Clear();

        var storagePets = G.Storage != null ? G.Storage.GetAllPetPrefabs() : null;
        AddValidBigPets(storagePets, _activePets);

        if (_activePets.Count == 0)
            AddValidBigPets(_pets, _activePets);

        _activePets.Sort(CompareBigPetAnimals);
        return _activePets.Count > 0;
    }

    private static void AddValidBigPets(IReadOnlyList<Brainrot> source, List<Brainrot> destination)
    {
        if (source == null || destination == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            var pet = source[i];
            if (!IsValidBigPetAnimal(pet))
                continue;

            if (!destination.Contains(pet))
                destination.Add(pet);
        }
    }

    private static bool IsValidBigPetAnimal(Brainrot pet)
    {
        if (pet == null)
            return false;

        if (ExcludedBigPetIds.Contains(GetPetId(pet)))
            return false;

        return pet.Data.StartIncome > 0d;
    }

    private static int CompareBigPetAnimals(Brainrot left, Brainrot right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;

        var incomeCompare = left.Data.StartIncome.CompareTo(right.Data.StartIncome);
        if (incomeCompare != 0)
            return incomeCompare;

        return string.Compare(GetPetId(left), GetPetId(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPetId(Brainrot pet)
    {
        if (pet == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(pet.Name))
            return pet.Name.Trim();

        if (!string.IsNullOrWhiteSpace(pet.gameObject.name))
            return pet.gameObject.name.Replace("(Clone)", string.Empty).Trim();

        return pet.name != null ? pet.name.Replace("(Clone)", string.Empty).Trim() : string.Empty;
    }

    private int GetMaxAvailablePetIndexForLevel(int level)
    {
        if (_activePets == null || _activePets.Count == 0)
            return 0;

        var levelsPerPet = Mathf.Max(1, _lvlsPerPet);
        var safeLevel = Mathf.Max(1, level);
        return Mathf.Clamp((safeLevel - 1) / levelsPerPet, 0, _activePets.Count - 1);
    }

    private static bool TryParseIncomeTimestamp(string raw, out long timestamp)
    {
        timestamp = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
            return true;

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            timestamp = dto.ToUnixTimeSeconds();
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dtInvariant))
        {
            timestamp = new DateTimeOffset(dtInvariant.ToUniversalTime()).ToUnixTimeSeconds();
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var dtCurrent))
        {
            timestamp = new DateTimeOffset(dtCurrent.ToUniversalTime()).ToUnixTimeSeconds();
            return true;
        }

        return false;
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

        ApplyLockedUiState(false);
    }

    private void ApplyLockedUiState(bool showBuyPanel)
    {
        SetQuickAccessBinding(false);

        if (_feedButton != null)
            _feedButton.SetActive(false);
        if (_foodTimeBar != null)
            _foodTimeBar.gameObject.SetActive(false);
        if (_foodTimeBarText != null)
            _foodTimeBarText.gameObject.SetActive(false);
        if (_xpProgressBar != null)
            _xpProgressBar.gameObject.SetActive(false);
        if (_xpProgressText != null)
            _xpProgressText.gameObject.SetActive(false);
        if (_setPetUI != null)
            _setPetUI.gameObject.SetActive(false);
        if (_petInfoUI != null)
            _petInfoUI.gameObject.SetActive(false);
        if (_changePetArea != null)
            _changePetArea.SetActive(false);

        if (_buyPanel != null)
        {
            _buyPanel.SetInfo(LocalizationUtils.T("Buy", "Купить"), Math.Round(_unlockPrice).ToString("0", CultureInfo.InvariantCulture));
            _buyPanel.gameObject.SetActive(showBuyPanel);
        }
    }

    private void EnsureIncomeRoutine()
    {
        if (_remoteMode || !_purchased || _incomeRoutine != null)
            return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
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

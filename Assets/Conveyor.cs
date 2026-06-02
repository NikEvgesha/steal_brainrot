using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour, IConveyorPercentSource
{
    [SerializeField] private bool _remoteMode;
    [SerializeField] private bool _openUiOnConveyorTrigger = true;
    [SerializeField] private float _spawnInterval;
    [SerializeField] private Material _mt;
    [SerializeField] private float _speed;
    [SerializeField] private float _matSpeedMultiplier;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _destroyPoint;
    [SerializeField] private LayerMask _egglayer;
    [SerializeField] private float _destroyDistance;
    [SerializeField] private ConveyorLevel _startLevel;
    [SerializeField] private ConveyorUI _ui;
    [SerializeField] private List<ConveyorLevel> _levels;

    private HashSet<Egg> _eggs;
    private ConveyorLevel _level;
    private int _currentLevel = 0;
    private int _lastUnlockedLevel = 0;
    private bool _initialized;
    private bool _localConfigured;
    private Coroutine _spawnRoutine;

    public float IncomeMultiplier => _level != null ? _level.IncomeMultiplier : 1f;
    public IReadOnlyList<ConveyorLevel> Levels => _levels;
    public ConveyorUI Ui => _ui;
    public bool IsRemoteMode => _remoteMode;
    public int CurrentLevelIndex => _currentLevel;
    public int UnlockedLevelIndex => _lastUnlockedLevel;
    public int MaxLevelIndex => _levels != null ? Mathf.Max(0, _levels.Count - 1) : 0;
    public float CurrentLevelProgress01 => MaxLevelIndex <= 0 ? 0f : Mathf.Clamp01(CurrentLevelIndex / (float)MaxLevelIndex);
    public float UnlockedLevelProgress01 => MaxLevelIndex <= 0 ? 0f : Mathf.Clamp01(UnlockedLevelIndex / (float)MaxLevelIndex);
    public float UnlockedIncomeMultiplier => GetUnlockedLevel() != null ? GetUnlockedLevel().IncomeMultiplier : 1f;
    public float UnlockedElementChanceBonus01 => GetUnlockedLevel() != null ? GetUnlockedLevel().ElementChanceBonus01 : 0f;

    private void Awake()
    {
        EnsureEggStorage();
        if (!_remoteMode)
            G.Initialized.AddListener(Init);
    }

    private void OnDisable()
    {
        StopSpawnLoop();
    }

    private void EnsureLocalInit()
    {
        if (_remoteMode) return;
        if (G.Save == null) return;
        if (_localConfigured && _initialized)
            return;

        Init();
    }

    private void EnsureEggStorage()
    {
        if (_eggs == null)
            _eggs = new HashSet<Egg>();
    }

    private void Init()
    {
        if (_remoteMode) return;
        if (G.Save == null) return;
        if (_levels == null || _levels.Count == 0)
        {
            Debug.LogWarning("[Conveyor] Levels list is empty.");
            return;
        }

        EnsureEggStorage();

        _currentLevel = Mathf.Clamp(G.Save.LoadConveyorCurrentLevel(), 0, _levels.Count - 1);
        _lastUnlockedLevel = Mathf.Clamp(G.Save.LoadConveyorUnlockedLevel(), 0, _levels.Count - 1);

        if (!_localConfigured)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (i > _lastUnlockedLevel)
                    _levels[i].LevelPurchased.AddListener(OnLevelPurchase);
            }

            _ui = _ui != null ? _ui : GetComponentInChildren<ConveyorUI>(true);
            if (_ui == null)
            {
                Debug.LogWarning("[Conveyor] ConveyorUI not found.");
                return;
            }

            _ui.Init(_levels);
            _ui.LevelActivated.AddListener(SetLevel);
            _localConfigured = true;
        }

        for (int i = 0; i < _levels.Count; i++)
        {
            var purchased = i <= _lastUnlockedLevel;
            _levels[i].SetPurchased(purchased);
            if (!purchased)
                _levels[i].SetPurchasingAvailable(i == _lastUnlockedLevel + 1);
        }

        SetLevel(_levels[_currentLevel]);
        _initialized = true;
        ShowLocalUI();
        EnableInteractionListeners(true);
        StartSpawnLoop();
    }

    private void FixedUpdate()
    {
        if (!_initialized) return;
        if (_destroyPoint == null || _eggs == null) return;

        if (_mt != null)
            _mt.mainTextureOffset = new Vector2(0, Time.time * _speed * _matSpeedMultiplier * Time.fixedDeltaTime);

        List<Egg> toRemove = null;
        foreach (Egg egg in _eggs)
        {
            if (egg == null)
            {
                if (toRemove == null) toRemove = new List<Egg>();
                toRemove.Add(egg);
                continue;
            }

            if (Vector3.Distance(egg.transform.position, _destroyPoint.position) < _destroyDistance)
            {
                if (toRemove == null) toRemove = new List<Egg>();
                toRemove.Add(egg);
            }
            else
            {
                egg.transform.position += egg.transform.forward * _speed * Time.fixedDeltaTime;
            }
        }

        if (toRemove == null) return;
        foreach (var egg in toRemove)
        {
            _eggs.Remove(egg);
            if (egg != null)
                Destroy(egg.gameObject);
        }
    }

    private IEnumerator Spawn()
    {
        while (enabled)
        {
            if (!_initialized || _level == null || _spawnPoint == null)
            {
                yield return null;
                continue;
            }

            var prefab = _level.GetRandomEgg();
            if (prefab != null)
            {
                Egg egg = Instantiate(prefab, _spawnPoint.position, _spawnPoint.rotation);
                egg.SetRandomData();
                egg.SetConveyorPurchaseMode(_remoteMode);
                _eggs.Add(egg);
                egg.EggPurchased.AddListener(OnEggPurchase);
            }

            yield return new WaitForSeconds(_spawnInterval);
        }
    }

    private void StartSpawnLoop()
    {
        if (!_initialized) return;
        if (_spawnRoutine != null) return;
        _spawnRoutine = StartCoroutine(Spawn());
    }

    private void StopSpawnLoop()
    {
        if (_spawnRoutine == null) return;
        StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
    }

    private void OnEggPurchase(Egg egg)
    {
        if (_eggs == null) return;
        _eggs.Remove(egg);
    }

    public float GetPercentBonus()
    {
        return Mathf.Max(0f, UnlockedIncomeMultiplier - 1f);
    }

    private ConveyorLevel GetUnlockedLevel()
    {
        if (_levels == null || _levels.Count == 0)
            return null;

        int index = Mathf.Clamp(_lastUnlockedLevel, 0, _levels.Count - 1);
        return _levels[index];
    }

    private void HideRemoteUI()
    {
        if (_ui == null) return;
        _ui.ToggleOpen(false);
        _ui.gameObject.SetActive(false);
    }

    private void ShowLocalUI()
    {
        if (_ui == null) return;
        _ui.gameObject.SetActive(true);
    }

    private void EnableInteractionListeners(bool enabled)
    {
        var listeners = GetComponentsInChildren<InteractionRaycastListener>(true);
        foreach (var listener in listeners)
            listener.enabled = enabled;
    }

    public void SetLevel(ConveyorLevel lvl)
    {
        if (lvl == null || _levels == null || _levels.Count == 0)
            return;

        if (_level != null)
        {
            _level.SetActive(false);
            _level.gameObject.SetActive(false);
        }

        _currentLevel = _levels.IndexOf(lvl);
        if (_currentLevel < 0) _currentLevel = 0;

        _level = _levels[_currentLevel];
        _level.SetActive(true);
        _level.gameObject.SetActive(true);

        if (!_remoteMode)
        {
            G.Save.SaveConveyorCurrentLevel(_currentLevel);
            if (_ui != null)
                _ui.UpdateActiveLvl(_currentLevel);
            G.Luck?.NotifyChanged();
            BaseDirtyTracker.MarkDirty();
        }
    }

    private void OnLevelPurchase(ConveyorLevel lvl)
    {
        if (_remoteMode) return;
        int id = _levels.IndexOf(lvl);
        if (id < 0) return;

        _lastUnlockedLevel = Mathf.Max(_lastUnlockedLevel, id);
        G.Save.SaveConveyorUnlockedLevel(_lastUnlockedLevel);
        SetLevel(lvl);
        G.Luck?.NotifyChanged();
        BaseDirtyTracker.MarkDirty();
        if (id < _levels.Count - 1)
            _levels[id + 1].SetPurchasingAvailable(true);
    }

    public void _OnPlayerEnter()
    {
        if (_remoteMode) return;
        if (!_openUiOnConveyorTrigger) return;
        _ui?.ToggleOpen(true);
    }

    public void _OnPlayerExit()
    {
        if (_remoteMode) return;
        if (!_openUiOnConveyorTrigger) return;
        _ui?.ToggleOpen(false);
    }

    public void ApplyRemoteLevel(int level)
    {
        _remoteMode = true;
        EnsureEggStorage();
        _initialized = true;
        HideRemoteUI();
        EnableInteractionListeners(false);

        if (_levels == null || _levels.Count == 0)
            return;

        level = Mathf.Clamp(level, 0, _levels.Count - 1);
        SetLevel(_levels[level]);
        StartSpawnLoop();
    }

    public void ClearSpawnedEggs()
    {
        if (_eggs == null || _eggs.Count == 0) return;

        foreach (var egg in _eggs)
        {
            if (egg != null)
                Destroy(egg.gameObject);
        }
        _eggs.Clear();
    }

    public void SetRemoteMode(bool remote)
    {
        if (_remoteMode == remote)
        {
            if (!_remoteMode)
                EnsureLocalInit();
            return;
        }

        _remoteMode = remote;
        StopSpawnLoop();
        ClearSpawnedEggs();

        if (_remoteMode)
        {
            _initialized = true;
            HideRemoteUI();
            EnableInteractionListeners(false);
        }
        else
        {
            _initialized = false;
            ShowLocalUI();
            EnableInteractionListeners(true);
            EnsureLocalInit();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour
{
    [SerializeField] private bool _remoteMode;
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

    public float IncomeMultiplier => _level.IncomeMultiplier;

    private void Awake()
    {
        if (!_remoteMode)
            G.Initialized.AddListener(Init);
    }


    private void EnsureLocalInit()
    {
        if (_initialized || _remoteMode) return;
        Init();
    }

    private void Init()
    {
        if (_remoteMode) return;
        _currentLevel = G.Save.LoadConveyorCurrentLevel();
        _lastUnlockedLevel = G.Save.LoadConveyorUnlockedLevel();

        for (int i = 0; i <= _lastUnlockedLevel; i++)
        {
            _levels[i].SetPurchased(true);
        }

        if (_levels == null || _levels.Count == 0)
        {
            Debug.LogWarning("[Conveyor] Levels list is empty.");
            return;
        }

        for (int i = _lastUnlockedLevel + 1; i < _levels.Count; i++)
        {
            _levels[i].LevelPurchased.AddListener(OnLevelPurchase);
            _levels[i].SetPurchasingAvailable(i == _lastUnlockedLevel + 1 ? true : false);
        }

        _ui = GetComponentInChildren<ConveyorUI>();
        if (_ui == null)
        {
            Debug.LogWarning("[Conveyor] ConveyorUI not found.");
            return;
        }
        _ui.Init(_levels);
        _ui.LevelActivated.AddListener(SetLevel);
        SetLevel(_levels[_currentLevel]);
        _eggs = new();
        _initialized = true;
        StartCoroutine(Spawn());
        
    }

    private void FixedUpdate()
    {
        if (!_initialized || _remoteMode) return;
        _mt.mainTextureOffset = new Vector2(0, Time.time * _speed * _matSpeedMultiplier * Time.fixedDeltaTime);


        Egg destroyEgg = null;
        
        foreach (Egg egg in _eggs)
        {
            if (Vector3.Distance(egg.transform.position, _destroyPoint.position) < _destroyDistance)
            {
                destroyEgg = egg;
            } else
            {
                egg.transform.position += egg.transform.forward * _speed * Time.fixedDeltaTime;
            }
                
        }

        if (destroyEgg)
        {
            _eggs.Remove(destroyEgg);
            Destroy(destroyEgg.gameObject);
        }
    }

    private IEnumerator Spawn()
    {
        while (enabled)
        {
            Egg egg = Instantiate(_level.GetRandomEgg(), _spawnPoint.position, _spawnPoint.rotation);
            egg.SetRandomData();
            _eggs.Add(egg);
            egg.EggPurchased.AddListener(OnEggPurchase);
            yield return new WaitForSeconds(_spawnInterval);
        }
    }

    private void OnEggPurchase(Egg egg)
    {
        _eggs.Remove(egg);
    }



    private void HideRemoteUI()
    {
        if (_ui != null) _ui.gameObject.SetActive(false);
    }


    private void ShowLocalUI()
    {
        if (_ui != null) _ui.gameObject.SetActive(true);
    }

    public void SetLevel(ConveyorLevel lvl)
    {
        if (_level != null)
        {
            _level.SetActive(false);
            _level.gameObject.SetActive(false);
        }
        _currentLevel = _levels.IndexOf(lvl);
        _level = lvl;
        _level.SetActive(true);
        _level.gameObject.SetActive(true);
        if (!_remoteMode)
        {
            G.Save.SaveConveyorCurrentLevel(_levels.IndexOf(lvl));
            _ui.UpdateActiveLvl(_currentLevel);
            BaseDirtyTracker.MarkDirty();
        }
    }

    private void OnLevelPurchase(ConveyorLevel lvl)
    {
        if (_remoteMode) return;
        int id = _levels.IndexOf(lvl);
        G.Save.SaveConveyorUnlockedLevel(id);
        BaseDirtyTracker.MarkDirty();
        if (id < _levels.Count - 1)
        {
            _levels[id + 1].SetPurchasingAvailable(true);
        }
    }


    public void _OnPlayerEnter()
    {
        if (_remoteMode) return;
        _ui.ToggleOpen(true);
    }


    public void _OnPlayerExit()
    {
        if (_remoteMode) return;
        _ui?.ToggleOpen(false);
    }

    public void ApplyRemoteLevel(int level)
    {
        _remoteMode = true;
        HideRemoteUI();
        StopAllCoroutines();
        _initialized = false;

        if (_ui != null)
            _ui.gameObject.SetActive(false);

        if (_levels == null || _levels.Count == 0)
            return;

        level = Mathf.Clamp(level, 0, _levels.Count - 1);
        SetLevel(_levels[level]);
        _initialized = true;
    }

    public void SetRemoteMode(bool remote)
    {
        _remoteMode = remote;
        if (_remoteMode)
        {
            HideRemoteUI();
        }
        else
        {
            ShowLocalUI();
            EnsureLocalInit();
        }
    }
}

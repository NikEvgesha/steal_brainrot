using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour
{
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
        G.Initialized.AddListener(Init);
    }

    private void Init()
    {
        _currentLevel = G.Save.LoadConveyorCurrentLevel();
        _lastUnlockedLevel = G.Save.LoadConveyorUnlockedLevel();

        for (int i = 0; i <= _lastUnlockedLevel; i++)
        {
            _levels[i].SetPurchased(true);
        }

        for (int i = _lastUnlockedLevel + 1; i < _levels.Count; i++)
        {
            _levels[i].LevelPurchased.AddListener(OnLevelPurchase);
            _levels[i].SetPurchasingAvailable(i == _lastUnlockedLevel + 1 ? true : false);
        }

        _ui = GetComponentInChildren<ConveyorUI>();
        _ui.Init(_levels);
        _ui.LevelActivated.AddListener(SetLevel);
        SetLevel(_levels[_currentLevel]);
        _eggs = new();
        _initialized = true;
        StartCoroutine(Spawn());
        
    }

    private void FixedUpdate()
    {
        if (!_initialized) return;
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
        G.Save.SaveConveyorCurrentLevel(_levels.IndexOf(lvl));
        _ui.UpdateActiveLvl(_currentLevel);
    }

    private void OnLevelPurchase(ConveyorLevel lvl)
    {
        int id = _levels.IndexOf(lvl);
        G.Save.SaveConveyorUnlockedLevel(id);
        if (id < _levels.Count - 1)
        {
            _levels[id + 1].SetPurchasingAvailable(true);
        }
    }


    public void _OnPlayerEnter()
    {
        _ui.ToggleOpen(true);
    }


    public void _OnPlayerExit()
    {
        _ui?.ToggleOpen(false);
    }
}

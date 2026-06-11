using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour, IConveyorPercentSource
{
    private const string BaseMapProperty = "_BaseMap";
    private const string MainTexProperty = "_MainTex";
    private static readonly int BaseMapStProperty = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexStProperty = Shader.PropertyToID("_MainTex_ST");
    private static readonly int BeltOffsetProperty = Shader.PropertyToID("_BeltOffset");
    private static readonly int BeltAxisProperty = Shader.PropertyToID("_BeltAxis");

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
    private float _beltScrollOffset;
    private MaterialPropertyBlock _beltPropertyBlock;
    private readonly List<BeltRendererBinding> _beltRendererBindings = new List<BeltRendererBinding>();

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
        CacheBeltRenderers();
        if (!_remoteMode)
            G.Initialized.AddListener(Init);
    }

    private void OnEnable()
    {
        if (_initialized)
            StartSpawnLoop();
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
                Debug.LogWarning("[Conveyor] ConveyorUI not found. Conveyor gameplay will continue without local UI.", this);
            }
            else
            {
                try
                {
                    _ui.Init(_levels);
                    _ui.LevelActivated.AddListener(SetLevel);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }

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

        ScrollBeltMaterial();

        if (_destroyPoint == null || _eggs == null) return;

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
            {
                try
                {
                    _ui.UpdateActiveLvl(_currentLevel);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
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

    private void CacheBeltRenderers()
    {
        _beltRendererBindings.Clear();

        _beltPropertyBlock ??= new MaterialPropertyBlock();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (!IsBeltRenderer(renderer))
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
            {
                _beltRendererBindings.Add(new BeltRendererBinding(
                    renderer,
                    j,
                    GetTextureScaleOffset(materials[j], BaseMapProperty),
                    GetTextureScaleOffset(materials[j], MainTexProperty),
                    GetBeltAxis(renderer)));
            }
        }

        if (_beltRendererBindings.Count > 0 || _mt == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
            {
                if (!IsSameMaterial(materials[j], _mt))
                    continue;

                _beltRendererBindings.Add(new BeltRendererBinding(
                    renderer,
                    j,
                    GetTextureScaleOffset(materials[j], BaseMapProperty),
                    GetTextureScaleOffset(materials[j], MainTexProperty),
                    GetBeltAxis(renderer)));
            }
        }
    }

    private void ScrollBeltMaterial()
    {
        if (_beltRendererBindings.Count == 0)
            CacheBeltRenderers();
        if (_beltRendererBindings.Count == 0)
            return;

        float scrollSpeed = _speed * _matSpeedMultiplier * Time.fixedDeltaTime;
        if (Mathf.Approximately(scrollSpeed, 0f))
            return;

        _beltScrollOffset = Mathf.Repeat(_beltScrollOffset + scrollSpeed * Time.fixedDeltaTime, 1f);

        for (int i = _beltRendererBindings.Count - 1; i >= 0; i--)
        {
            BeltRendererBinding binding = _beltRendererBindings[i];
            if (binding.Renderer == null)
            {
                _beltRendererBindings.RemoveAt(i);
                continue;
            }

            Vector4 baseMap = binding.BaseMapScaleOffset;
            Vector4 mainTex = binding.MainTexScaleOffset;
            baseMap.w += _beltScrollOffset;
            mainTex.w += _beltScrollOffset;

            binding.Renderer.GetPropertyBlock(_beltPropertyBlock, binding.MaterialIndex);
            _beltPropertyBlock.SetFloat(BeltOffsetProperty, _beltScrollOffset);
            _beltPropertyBlock.SetVector(BeltAxisProperty, binding.Axis);
            _beltPropertyBlock.SetVector(BaseMapStProperty, baseMap);
            _beltPropertyBlock.SetVector(MainTexStProperty, mainTex);
            binding.Renderer.SetPropertyBlock(_beltPropertyBlock, binding.MaterialIndex);
        }
    }

    private static Vector4 GetTextureScaleOffset(Material material, string textureProperty)
    {
        if (material == null || !material.HasProperty(textureProperty))
            return new Vector4(1f, 1f, 0f, 0f);

        Vector2 scale = material.GetTextureScale(textureProperty);
        Vector2 offset = material.GetTextureOffset(textureProperty);
        return new Vector4(scale.x, scale.y, offset.x, offset.y);
    }

    private static bool IsSameMaterial(Material candidate, Material reference)
    {
        if (candidate == null || reference == null)
            return false;
        if (candidate == reference)
            return true;

        return string.Equals(candidate.name, reference.name, StringComparison.Ordinal);
    }

    private static bool IsBeltRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;
        if (!string.Equals(renderer.gameObject.name, "belt", StringComparison.OrdinalIgnoreCase))
            return false;

        Transform parent = renderer.transform.parent;
        while (parent != null)
        {
            if (string.Equals(parent.name, "belt", StringComparison.OrdinalIgnoreCase))
                return true;
            parent = parent.parent;
        }

        return false;
    }

    private static Vector4 GetBeltAxis(Renderer renderer)
    {
        MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
        Vector3 size = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds.size
            : Vector3.one;

        if (size.y >= size.x && size.y >= size.z)
            return new Vector4(0f, 1f, 0f, 0f);
        if (size.z >= size.x && size.z >= size.y)
            return new Vector4(0f, 0f, 1f, 0f);

        return new Vector4(1f, 0f, 0f, 0f);
    }

    private readonly struct BeltRendererBinding
    {
        public readonly Renderer Renderer;
        public readonly int MaterialIndex;
        public readonly Vector4 BaseMapScaleOffset;
        public readonly Vector4 MainTexScaleOffset;
        public readonly Vector4 Axis;

        public BeltRendererBinding(
            Renderer renderer,
            int materialIndex,
            Vector4 baseMapScaleOffset,
            Vector4 mainTexScaleOffset,
            Vector4 axis)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
            BaseMapScaleOffset = baseMapScaleOffset;
            MainTexScaleOffset = mainTexScaleOffset;
            Axis = axis;
        }
    }
}

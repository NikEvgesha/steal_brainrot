using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour, IConveyorPercentSource
{
    private const string BaseMapProperty = "_BaseMap";
    private const string MainTexProperty = "_MainTex";
    private const string LevelsAreaName = "LevelsArea";
    private static readonly int BaseMapStProperty = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexStProperty = Shader.PropertyToID("_MainTex_ST");
    private static readonly int BeltOffsetProperty = Shader.PropertyToID("_BeltOffset");
    private static readonly int BeltAxisProperty = Shader.PropertyToID("_BeltAxis");
    private static readonly int BeltSideAxisProperty = Shader.PropertyToID("_BeltSideAxis");
    private static readonly int BeltHalfWidthProperty = Shader.PropertyToID("_BeltHalfWidth");

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
    [SerializeField] private GameObject _levelsArea;
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
        ResolveLevelsArea();
        CacheBeltRenderers();
        if (!_remoteMode)
            G.Initialized.AddListener(Init);
        else
            HideRemoteUI();
    }

    private void OnEnable()
    {
        EnsureEggStorage();
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
                EnsureEggStorage();
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
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;
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

    public Egg FindNearestAvailableEgg(Vector3 origin, string requiredId = null)
    {
        EnsureEggStorage();
        Egg nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Egg egg in _eggs)
        {
            if (egg == null || egg.Status != EggStatus.Conveyer || !egg.gameObject.activeInHierarchy)
                continue;
            if (!string.IsNullOrEmpty(requiredId) && !string.Equals(egg.Name, requiredId, StringComparison.OrdinalIgnoreCase))
                continue;

            float distance = (egg.transform.position - origin).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearest = egg;
            nearestDistance = distance;
        }

        return nearest;
    }

    public bool IsTrackedEgg(Egg egg)
    {
        return egg != null && _eggs != null && _eggs.Contains(egg);
    }

    public Egg SpawnTutorialEgg(Egg prefab)
    {
        if (_remoteMode || prefab == null || _spawnPoint == null)
            return null;

        EnsureEggStorage();
        Egg egg = Instantiate(prefab, _spawnPoint.position, _spawnPoint.rotation);
        egg.SetConveyorPurchaseMode(false);
        _eggs.Add(egg);
        egg.EggPurchased.AddListener(OnEggPurchase);
        return egg;
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
        if (_ui != null)
        {
            _ui.ToggleOpen(false);
            _ui.gameObject.SetActive(false);
        }

        SetLevelsAreaVisible(false);
    }

    private void ShowLocalUI()
    {
        if (_ui != null)
            _ui.gameObject.SetActive(true);

        SetLevelsAreaVisible(true);
    }

    private void SetLevelsAreaVisible(bool visible)
    {
        GameObject levelsArea = ResolveLevelsArea();
        if (levelsArea != null)
            levelsArea.SetActive(visible);
    }

    private GameObject ResolveLevelsArea()
    {
        if (_levelsArea != null)
            return _levelsArea;

        Transform levelsAreaTransform = FindChildByName(transform, LevelsAreaName);
        if (levelsAreaTransform != null)
            _levelsArea = levelsAreaTransform.gameObject;

        return _levelsArea;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.Ordinal))
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void EnableInteractionListeners(bool enabled)
    {
        var listeners = GetComponentsInChildren<InteractionRaycastListener>(true);
        foreach (var listener in listeners)
        {
            listener.SetZoneVisualSuppressed(!enabled);
            listener.enabled = enabled;
        }

        var visuals = GetComponentsInChildren<InteractionZoneVisual>(true);
        foreach (var visual in visuals)
            visual.SetVisible(enabled);
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
        else
        {
            EnableInteractionListeners(false);
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
        TutorialSignals.Raise(TutorialSignalType.ConveyorUpgraded, this, id.ToString(), Item.Free, id);
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
        EnableInteractionListeners(false);
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
            else
            {
                HideRemoteUI();
                EnableInteractionListeners(false);
            }
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
        Vector3 beltMoveDirection = GetBeltMoveDirection();

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
                Vector4 axis = GetBeltAxis(renderer, beltMoveDirection);
                _beltRendererBindings.Add(new BeltRendererBinding(
                    renderer,
                    j,
                    GetTextureScaleOffset(materials[j], BaseMapProperty),
                    GetTextureScaleOffset(materials[j], MainTexProperty),
                    axis,
                    GetBeltSideAxis(axis),
                    GetBeltHalfWidth(renderer, axis)));
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

                Vector4 axis = GetBeltAxis(renderer, beltMoveDirection);
                _beltRendererBindings.Add(new BeltRendererBinding(
                    renderer,
                    j,
                    GetTextureScaleOffset(materials[j], BaseMapProperty),
                    GetTextureScaleOffset(materials[j], MainTexProperty),
                    axis,
                    GetBeltSideAxis(axis),
                    GetBeltHalfWidth(renderer, axis)));
            }
        }
    }

    private Vector3 GetBeltMoveDirection()
    {
        Vector3 direction = _spawnPoint != null ? _spawnPoint.forward : Vector3.zero;

        if (direction.sqrMagnitude < 0.0001f && _spawnPoint != null && _destroyPoint != null)
            direction = _destroyPoint.position - _spawnPoint.position;

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.right;

        return direction.normalized;
    }

    private void ScrollBeltMaterial()
    {
        if (_beltRendererBindings.Count == 0)
            CacheBeltRenderers();
        if (_beltRendererBindings.Count == 0)
            return;

        float scrollStep = _speed * _matSpeedMultiplier * Time.fixedDeltaTime;
        if (Mathf.Approximately(scrollStep, 0f))
            return;

        _beltScrollOffset = Mathf.Repeat(_beltScrollOffset + scrollStep, 1f);

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
            _beltPropertyBlock.SetVector(BeltSideAxisProperty, binding.SideAxis);
            _beltPropertyBlock.SetFloat(BeltHalfWidthProperty, binding.HalfWidth);
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

    private static Vector4 GetBeltAxis(Renderer renderer, Vector3 desiredWorldDirection)
    {
        MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
        Vector3 size = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds.size
            : Vector3.one;

        Vector3 localAxis;
        if (size.y >= size.x && size.y >= size.z)
            localAxis = Vector3.up;
        else if (size.z >= size.x && size.z >= size.y)
            localAxis = Vector3.forward;
        else
            localAxis = Vector3.right;

        if (renderer != null && desiredWorldDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 worldAxis = renderer.transform.TransformDirection(localAxis);
            if (Vector3.Dot(worldAxis, desiredWorldDirection) < 0f)
                localAxis = -localAxis;
        }

        return new Vector4(localAxis.x, localAxis.y, localAxis.z, 0f);
    }

    private static Vector4 GetBeltSideAxis(Vector4 axis)
    {
        Vector3 side = Vector3.Cross(Vector3.up, new Vector3(axis.x, axis.y, axis.z));
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.forward;

        side.Normalize();
        return new Vector4(side.x, side.y, side.z, 0f);
    }

    private static float GetBeltHalfWidth(Renderer renderer, Vector4 axis)
    {
        MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
        Vector3 size = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds.size
            : Vector3.one;

        if (Mathf.Abs(axis.x) > 0.5f)
            return Mathf.Max(0.05f, size.z * 0.5f);
        if (Mathf.Abs(axis.z) > 0.5f)
            return Mathf.Max(0.05f, size.x * 0.5f);

        return Mathf.Max(0.05f, Mathf.Max(size.x, size.z) * 0.5f);
    }

    private readonly struct BeltRendererBinding
    {
        public readonly Renderer Renderer;
        public readonly int MaterialIndex;
        public readonly Vector4 BaseMapScaleOffset;
        public readonly Vector4 MainTexScaleOffset;
        public readonly Vector4 Axis;
        public readonly Vector4 SideAxis;
        public readonly float HalfWidth;

        public BeltRendererBinding(
            Renderer renderer,
            int materialIndex,
            Vector4 baseMapScaleOffset,
            Vector4 mainTexScaleOffset,
            Vector4 axis,
            Vector4 sideAxis,
            float halfWidth)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
            BaseMapScaleOffset = baseMapScaleOffset;
            MainTexScaleOffset = mainTexScaleOffset;
            Axis = axis;
            SideAxis = sideAxis;
            HalfWidth = halfWidth;
        }
    }
}

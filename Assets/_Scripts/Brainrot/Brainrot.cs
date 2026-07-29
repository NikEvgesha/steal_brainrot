using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct BrainrotTypeData
{
    //public GameObject Model;

    public double StartIncome;
    public float MinWeight;
    public float MaxWeightMult;

    public double StartSellPrice;
}


[Serializable]
public struct BrainrotDinamicData
{
    public ElementType ElementType;
    public float WeightMultiplier;
    public double ResultIncome;
}
public class Brainrot : InventoryItem
{
    private const int ElementRayDefaultsVersion = 1;
    private const float DefaultElementRayAlphaMultiplier = 0.5f;
    private const float DefaultElementRayLength = 2.5f;
    private const float DefaultElementRayThickness = 4f;
    private const float DefaultElementRaySpinSpeed = 35f;

    [SerializeField] private Transform _modelPoint;
    [SerializeField] private AudioSource _audio;

    [SerializeField] private BrainrotTypeData _data;
    [Header("Element VFX")]
    [SerializeField] private float _elementParticleSizeMultiplier = 1f;
    [SerializeField] private float _elementParticleEmissionMultiplier = 1f;
    [SerializeField] private float _elementParticleRadiusMultiplier = 1f;
    [SerializeField] private float _elementRayAlphaMultiplier = DefaultElementRayAlphaMultiplier;
    [SerializeField] private float _elementRayLength = DefaultElementRayLength;
    [SerializeField] private float _elementRayThickness = DefaultElementRayThickness;
    [SerializeField] private float _elementRaySpinSpeed = DefaultElementRaySpinSpeed;
    [SerializeField] private float _elementGroundAlphaMultiplier = 1f;
    [SerializeField] private float _elementGroundRadius = 0f;
    [SerializeField] private float _elementGroundYOffset = 0.02f;
    [SerializeField, HideInInspector] private int _elementRayDefaultsVersion;

    public BrainrotTypeData Data => _data;

    private BrainrotDinamicData _dinamicData;
    public BrainrotDinamicData DinamicData => _dinamicData;

    public GameObject Model => _modelPoint.gameObject;
    public double CurrentIncome => _currentIncome;
    public double MaxAccumulatedIncome =>
        Math.Max(0d, _dinamicData.ResultIncome * OfflineRewardRules.MaxAccrualSeconds);
    public long LastIncomeCollectTime => _lastIncomeTime;
    public bool HasCollectibleIncome => IsCollectibleLocal() && _currentIncome > 0d;

    private BrainrotInfoUI _canvas;
    private GameObject _model;

    private double _currentIncome;
    private long _lastIncomeTime;

    private FieldCell _floorListener;


    public Action Selled;
    public Action Stealed;
    private Coroutine _incomeCorutine;
    private bool _incomeReadySignaled;
    private bool _hasOfflineIncomePending;
    private Animator[] _animators;
    private Animation[] _legacyAnimations;
    private bool _animationsSubscribed;
    private Coroutine _disableAnimationsRoutine;

    private struct AnimatorStopTarget
    {
        public Animator animator;
        public int layer;
        public int stateHash;
        public float targetNormalizedTime;
    }

    private void OnEnable()
    {
        EnsureAnimationsSubscription();
        ApplyAnimalsAnimationState();
        StartIncomeRoutineIfNeeded();
    }

    private void OnDisable()
    {
        StopIncomeRoutine();
        StopPendingAnimationsDisable();
        ReleaseAnimationsSubscription();
    }

    private void OnValidate()
    {
        ApplyElementRayDefaultsIfNeeded();
        ClampElementVfxSettings();

        if (Application.isPlaying && isActiveAndEnabled)
            RefreshElementVfx();
    }

    [ContextMenu("Refresh Element VFX")]
    private void RefreshElementVfx()
    {
        ClampElementVfxSettings();
        if (!Application.isPlaying || _modelPoint == null)
            return;

        ApplyElementVfx();
    }

    private void ClampElementVfxSettings()
    {
        _elementParticleSizeMultiplier = Mathf.Max(0.1f, _elementParticleSizeMultiplier);
        _elementParticleEmissionMultiplier = Mathf.Max(0.1f, _elementParticleEmissionMultiplier);
        _elementParticleRadiusMultiplier = Mathf.Max(0.1f, _elementParticleRadiusMultiplier);
        _elementRayAlphaMultiplier = Mathf.Max(0f, _elementRayAlphaMultiplier);
        _elementRayLength = Mathf.Max(0f, _elementRayLength);
        _elementRayThickness = Mathf.Max(0f, _elementRayThickness);
        _elementGroundAlphaMultiplier = Mathf.Max(0f, _elementGroundAlphaMultiplier);
        _elementGroundRadius = Mathf.Max(0f, _elementGroundRadius);
    }

    private void ApplyElementRayDefaultsIfNeeded()
    {
        if (_elementRayDefaultsVersion >= ElementRayDefaultsVersion)
            return;

        _elementRayAlphaMultiplier = DefaultElementRayAlphaMultiplier;
        _elementRayLength = DefaultElementRayLength;
        _elementRayThickness = DefaultElementRayThickness;
        _elementRaySpinSpeed = DefaultElementRaySpinSpeed;
        _elementRayDefaultsVersion = ElementRayDefaultsVersion;
    }

    public void Init(BrainrotDinamicData rarity, FieldCell floor=null, long lastCollectTimestamp = -1) //передавать плейс из яйца
    {
        ApplyElementRayDefaultsIfNeeded();

        // rarity считается в яйце? 
        _canvas = GetComponentInChildren<BrainrotInfoUI>(true);
        CacheAnimationComponents();
        EnsureAnimationsSubscription();
        _dinamicData = rarity;
        //_model = Instantiate(_data.,_modelPoint);
        Vector3 scale = _canvas != null ? _canvas.transform.localScale : Vector3.one;
        if (_canvas != null && _modelPoint != null)
            _canvas.transform.SetParent(_modelPoint.transform, false);
        SetSize();
        if (_canvas != null)
        {
            _canvas.transform.SetParent(transform, false);
            _canvas.transform.localScale = scale;
        }

        var elementMultiplier = G.Elements != null ? G.Elements.GetMultiplaer(_dinamicData.ElementType) : 1f;
        _dinamicData.ResultIncome = Math.Round(_data.StartIncome * elementMultiplier * (_dinamicData.WeightMultiplier / 2));
        var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var effectiveLastIncomeTs = lastCollectTimestamp > 0 ? lastCollectTimestamp : nowTs;
        if (effectiveLastIncomeTs > nowTs)
            effectiveLastIncomeTs = nowTs;

        _lastIncomeTime = effectiveLastIncomeTs;
        var incomeAccumulationTime = OfflineRewardRules.ClampAccrualSeconds(nowTs - effectiveLastIncomeTs);
        _currentIncome = Math.Max(0d, Math.Round(incomeAccumulationTime * _dinamicData.ResultIncome));
        _hasOfflineIncomePending = incomeAccumulationTime >= 60L && _currentIncome > 0d;
        _incomeReadySignaled = _currentIncome > 0d;
        if (floor != null)
            NewPlace(floor);
        if (_canvas != null)
            _canvas.SetInfo(_data, _dinamicData);
        if (_canvas != null && incomeAccumulationTime > 0)
            _canvas.UpdateOfflineIncome(_currentIncome);
        SetTypeVisual();
        ApplyElementVfx();
        ApplyAnimalsAnimationState();
        //_floorListener._hitEvent.AddListener(PlayerInPlace);
    }

    private void ApplyElementVfx()
    {
        ElementTypeVfx.Ensure(
            this,
            _modelPoint,
            _dinamicData.ElementType,
            sizeMultiplier: _elementParticleSizeMultiplier,
            emissionMultiplier: _elementParticleEmissionMultiplier,
            rayAlphaMultiplier: _elementRayAlphaMultiplier,
            particleRadiusMultiplier: _elementParticleRadiusMultiplier,
            rayLengthWorldOverride: _elementRayLength,
            rayThicknessWorldOverride: _elementRayThickness,
            raySpinSpeed: _elementRaySpinSpeed,
            groundAlphaMultiplier: _elementGroundAlphaMultiplier,
            groundRadiusWorldOverride: _elementGroundRadius,
            groundYOffsetWorld: _elementGroundYOffset);
    }

    private void SetTypeVisual()
    {
        if (_modelPoint == null)
            return;

        Renderer[] renderer = _modelPoint.GetComponentsInChildren<Renderer>();
        foreach (Renderer item in renderer)
        {
            switch (_dinamicData.ElementType)
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
        if (_floorListener != null)
        {
            _floorListener.PlayerEnter.RemoveListener(PlayerInPlace);
            _floorListener.TakeBrainrot.RemoveListener(TakeBrainrot);
        }

        _floorListener = floor;
        StartIncomeRoutineIfNeeded();

        if (floor == null)
            return;

        floor.PlayerEnter.RemoveListener(PlayerInPlace);
        floor.TakeBrainrot.RemoveListener(TakeBrainrot);
        floor.PlayerEnter.AddListener(PlayerInPlace);
        floor.TakeBrainrot.AddListener(TakeBrainrot);
    }
    private void SetSize()
    {
        if (_modelPoint == null)
            return;

        var m = Mathf.Max(1f, _dinamicData.WeightMultiplier); // �� ������ ������ �� ������ 1
        float scale = 1f + (m - 1f) * 0.25f;
        _modelPoint.transform.localScale = Vector3.one * scale;
    }

    private void PlayerInPlace()
    {
        GetIncome();
    }
    private void TakeBrainrot()
    {
        if (_floorListener != null)
        {
            _floorListener.PlayerEnter.RemoveListener(PlayerInPlace);
            _floorListener.TakeBrainrot.RemoveListener(TakeBrainrot);
            _floorListener.UpdateFieldItem(Item.Free);
        }

        StopIncomeRoutine();
        G.Inventory.Add(this);
        //TestBackpackBrainrot.Instance.TakeBrainrot(this);
    }
    private void GetIncome()
    {
        CollectIncome();
    }

    public double CollectIncome(bool playAudio = true)
    {
        if (!IsCollectibleLocal())
            return 0d;

        var collected = Math.Max(0d, _currentIncome);
        if (collected <= 0d)
            return 0d;

        bool hadOfflineIncome = _hasOfflineIncomePending;
        bool playOfflineIncome = playAudio && hadOfflineIncome;
        if (!TryAddCoins(collected, playAudio && !playOfflineIncome))
            return 0d;
        if (playOfflineIncome)
            G.Sound?.Play(GameAudioId.SFX_OFFLINE_INCOME);
        _hasOfflineIncomePending = false;

        _currentIncome = 0d;
        _lastIncomeTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (_floorListener != null)
            _floorListener.SaveData();
        if (_canvas != null)
            _canvas.UpdateIncome(_currentIncome);
        if (playAudio && G.Sound == null && _audio)
            _audio.Play();

        TutorialSignals.Raise(
            TutorialSignalType.IncomeCollected,
            _floorListener,
            Name,
            Item.Brainrot,
            collected);

        var incomeParameters = GameAnalytics.Params(
            "source_type", "brainrot",
            "source_id", Name,
            "amount", collected,
            "collection_mode", AnalyticsContext.IsIncomeBatch ? AnalyticsContext.IncomeBatchMode : "manual",
            "offline_income", hadOfflineIncome,
            "currency_type", "coins",
            "result", "success");
        GameAnalytics.TrackOnce("first_income_collected", AnalyticsEventNames.FirstIncomeCollected, incomeParameters);
        if (!AnalyticsContext.IsIncomeBatch)
            GameAnalytics.Track(AnalyticsEventNames.IncomeCollected, incomeParameters);

        return collected;
    }

    public void RefreshAccumulatedIncomeFromClock()
    {
        if (!IsCollectibleLocal())
            return;

        long nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_lastIncomeTime <= 0L || _lastIncomeTime > nowTs)
            _lastIncomeTime = nowTs;

        long elapsed = OfflineRewardRules.ClampAccrualSeconds(nowTs - _lastIncomeTime);
        _currentIncome = Math.Max(0d, Math.Round(elapsed * _dinamicData.ResultIncome));
        _currentIncome = double.IsInfinity(_currentIncome)
            ? float.MaxValue
            : Math.Min(_currentIncome, MaxAccumulatedIncome);
        _hasOfflineIncomePending = elapsed >= 60L && _currentIncome > 0d;
        _incomeReadySignaled = _currentIncome > 0d;

        if (_canvas != null)
        {
            _canvas.UpdateIncome(_currentIncome);
            if (elapsed > 0L)
                _canvas.UpdateOfflineIncome(_currentIncome);
        }
    }

    private bool IsCollectibleLocal()
    {
        var floor = ResolveFloorListener();
        if (floor == null)
            return false;

        var field = floor.GetComponentInParent<Field>();
        return field != null && !field.IsRemoteMode;
    }

    private FieldCell ResolveFloorListener()
    {
        if (_floorListener != null)
            return _floorListener;

        var floor = GetComponentInParent<FieldCell>();
        if (floor == null)
            return null;

        _floorListener = floor;
        _floorListener.PlayerEnter.RemoveListener(PlayerInPlace);
        _floorListener.TakeBrainrot.RemoveListener(TakeBrainrot);
        _floorListener.PlayerEnter.AddListener(PlayerInPlace);
        _floorListener.TakeBrainrot.AddListener(TakeBrainrot);
        return _floorListener;
    }

    private static bool TryAddCoins(double amount, bool playAudio)
    {
        if (amount <= 0d)
            return false;

        if (G.Income != null)
            return G.Income.TryAddCoins(amount, playAudio);

        if (G.Currency != null)
        {
            G.Currency.AddCurrency(CurrencyType.Coins, amount, playAudio);
            return true;
        }

        Debug.LogWarning("[Brainrot] Cannot collect income: income and currency managers are not initialized.");
        return false;
    }

    private IEnumerator ProduceIncome()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1);
            if (!IsCollectibleLocal())
                continue;

            _currentIncome += _dinamicData.ResultIncome; //2 is the magic number
            _currentIncome = double.IsInfinity(_currentIncome)
                ? float.MaxValue
                : Math.Min(_currentIncome, MaxAccumulatedIncome);
            _currentIncome = Math.Round(_currentIncome);
            if (!_incomeReadySignaled && _currentIncome > 0d)
            {
                _incomeReadySignaled = true;
                TutorialSignals.Raise(
                    TutorialSignalType.IncomeReady,
                    _floorListener,
                    Name,
                    Item.Brainrot,
                    _currentIncome);
            }
            if (_canvas != null)
                _canvas.UpdateIncome(_currentIncome);
        }
    }

    private void StartIncomeRoutineIfNeeded()
    {
        if (_incomeCorutine != null)
            return;
        if (!IsCollectibleLocal())
            return;
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        _incomeCorutine = StartCoroutine(ProduceIncome());
    }

    private void StopIncomeRoutine()
    {
        if (_incomeCorutine == null)
            return;

        StopCoroutine(_incomeCorutine);
        _incomeCorutine = null;
    }

    private void EnsureAnimationsSubscription()
    {
        if (_animationsSubscribed || G.Settings == null)
            return;

        G.Settings.ChangeAnimalsAnimations += OnAnimalsAnimationsChanged;
        _animationsSubscribed = true;
    }

    private void ReleaseAnimationsSubscription()
    {
        if (!_animationsSubscribed || G.Settings == null)
            return;

        G.Settings.ChangeAnimalsAnimations -= OnAnimalsAnimationsChanged;
        _animationsSubscribed = false;
    }

    private void OnAnimalsAnimationsChanged(bool isEnabled)
    {
        CacheAnimationComponents();
        ApplyAnimationsEnabled(isEnabled);
    }

    private void ApplyAnimalsAnimationState()
    {
        CacheAnimationComponents();
        bool isEnabled = G.Settings == null || G.Settings.AnimalsAnimationsEnabled;
        ApplyAnimationsEnabled(isEnabled);
    }

    private void CacheAnimationComponents()
    {
        _animators = GetComponentsInChildren<Animator>(true);
        _legacyAnimations = GetComponentsInChildren<Animation>(true);
    }

    private void ApplyAnimationsEnabled(bool isEnabled)
    {
        StopPendingAnimationsDisable();

        if (!isEnabled)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            _disableAnimationsRoutine = StartCoroutine(DisableAnimationsAfterCurrentLoop());
            return;
        }

        SetAnimationComponentsEnabled(true);
    }

    private IEnumerator DisableAnimationsAfterCurrentLoop()
    {
        SetAnimationComponentsEnabled(true);

        var targets = BuildAnimatorStopTargets();
        var legacyWaitUntil = Time.time + GetLegacyAnimationsRemainingTime();

        while (!AllAnimatorTargetsReached(targets) || Time.time < legacyWaitUntil)
            yield return null;

        ResetAnimationsToStart(targets);
        SetAnimationComponentsEnabled(false);
        _disableAnimationsRoutine = null;
    }

    private List<AnimatorStopTarget> BuildAnimatorStopTargets()
    {
        var targets = new List<AnimatorStopTarget>();
        if (_animators == null)
            return targets;

        for (int i = 0; i < _animators.Length; i++)
        {
            var animator = _animators[i];
            if (animator == null || !animator.gameObject.activeInHierarchy)
                continue;
            if (Mathf.Abs(animator.speed) <= 0.0001f)
                continue;

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                var state = animator.GetCurrentAnimatorStateInfo(layer);
                if (state.fullPathHash == 0)
                    continue;

                var normalizedTime = state.normalizedTime;
                var targetNormalizedTime = state.loop
                    ? Mathf.Floor(normalizedTime) + 1f
                    : 1f;

                targets.Add(new AnimatorStopTarget
                {
                    animator = animator,
                    layer = layer,
                    stateHash = state.fullPathHash,
                    targetNormalizedTime = Mathf.Max(targetNormalizedTime, normalizedTime)
                });
            }
        }

        return targets;
    }

    private static bool AllAnimatorTargetsReached(List<AnimatorStopTarget> targets)
    {
        if (targets == null || targets.Count == 0)
            return true;

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var animator = target.animator;
            if (animator == null || !animator.gameObject.activeInHierarchy)
                continue;

            var state = animator.GetCurrentAnimatorStateInfo(target.layer);
            if (state.fullPathHash != target.stateHash)
                continue;

            if (state.normalizedTime < target.targetNormalizedTime)
                return false;
        }

        return true;
    }

    private float GetLegacyAnimationsRemainingTime()
    {
        var maxRemaining = 0f;
        if (_legacyAnimations == null)
            return maxRemaining;

        for (int i = 0; i < _legacyAnimations.Length; i++)
        {
            var legacyAnimation = _legacyAnimations[i];
            if (legacyAnimation == null || !legacyAnimation.gameObject.activeInHierarchy)
                continue;

            foreach (AnimationState state in legacyAnimation)
            {
                if (state == null || !legacyAnimation.IsPlaying(state.name) || state.length <= 0f)
                    continue;

                var speed = Mathf.Abs(state.speed);
                if (speed <= 0.0001f)
                    continue;

                var normalizedTime = state.normalizedTime;
                var remainingNormalized = state.wrapMode == WrapMode.Loop
                    ? 1f - Mathf.Repeat(normalizedTime, 1f)
                    : Mathf.Max(0f, 1f - normalizedTime);
                maxRemaining = Mathf.Max(maxRemaining, remainingNormalized * state.length / speed);
            }
        }

        return maxRemaining;
    }

    private void ResetAnimationsToStart(List<AnimatorStopTarget> targets)
    {
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.animator == null || !target.animator.gameObject.activeInHierarchy)
                    continue;

                target.animator.Play(target.stateHash, target.layer, 0f);
                target.animator.Update(0f);
            }
        }

        if (_legacyAnimations == null)
            return;

        for (int i = 0; i < _legacyAnimations.Length; i++)
        {
            var legacyAnimation = _legacyAnimations[i];
            if (legacyAnimation == null || !legacyAnimation.gameObject.activeInHierarchy)
                continue;

            foreach (AnimationState state in legacyAnimation)
            {
                if (state == null)
                    continue;

                state.normalizedTime = 0f;
                legacyAnimation.Sample();
            }
        }
    }

    private void SetAnimationComponentsEnabled(bool isEnabled)
    {
        if (_animators != null)
        {
            for (int i = 0; i < _animators.Length; i++)
            {
                if (_animators[i] != null)
                    _animators[i].enabled = isEnabled;
            }
        }

        if (_legacyAnimations != null)
        {
            for (int i = 0; i < _legacyAnimations.Length; i++)
            {
                if (_legacyAnimations[i] != null)
                    _legacyAnimations[i].enabled = isEnabled;
            }
        }
    }

    private void StopPendingAnimationsDisable()
    {
        if (_disableAnimationsRoutine == null)
            return;

        StopCoroutine(_disableAnimationsRoutine);
        _disableAnimationsRoutine = null;
    }
}

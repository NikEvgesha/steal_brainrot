using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialManager : MonoBehaviour
{
    private const string ViewResourcePath = "Tutorial/TutorialView";
    private const string StarterEggId = "egg1";
    private const string StarterAnimalId = "Capybara";
    private const float MovementDistanceRequired = 5f;
    private const float TargetReachDistance = 7f;
    private const float MinimumTargetStepDisplaySeconds = 1.1f;
    private const int StarterHatchDurationSeconds = 5;
    private const float PostTutorialInterstitialGraceSeconds = 45f;

    public static TutorialManager Instance { get; private set; }

    private TutorialSaveData _state;
    private TutorialStepDefinition _currentDefinition;
    private TutorialTaskSaveData _currentTaskState;
    private TutorialView _view;
    private TutorialTargetHighlighter _highlighter;
    private TutorialUiBlocker _uiBlocker;
    private RemoteBasesApplier _remoteBases;
    private Transform _currentTarget;
    private Vector3 _lastMovementPosition;
    private float _movementDistance;
    private float _stepActivatedRealtime;
    private float _nextTargetRefresh;
    private float _nextActivationScan;
    private float _lastSavedMovementProgress;
    private bool _active;
    private bool _schedulerReady;
    private bool _processingTransition;
    private float _nextStarterOfferAttempt;
    private bool _starterOfferErrorLogged;
    private Egg _starterOfferEgg;
    private Coroutine _startRoutine;
#if UNITY_EDITOR
    private bool _editorDebugFreezeProgress;
#endif

    public bool IsTutorialActive => _active;
    public bool AllowsAlbum => _active && _state != null && CurrentStep == TutorialStepId.ClaimAlbumReward;
    public Transform CurrentTarget => _currentTarget;
    public TutorialStepId CurrentStep => _currentDefinition != null
        ? _currentDefinition.id
        : TutorialStepId.LearnMovement;
    public string CurrentStableId => _currentDefinition != null ? _currentDefinition.stableId : string.Empty;
    private int CurrentStepIndex => _currentDefinition != null
        ? Mathf.Max(0, TutorialStepCatalog.FindIndex(_currentDefinition.stableId, 0))
        : 0;

    public static TutorialManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        TutorialManager existing = FindAnyObjectByType<TutorialManager>();
        if (existing != null)
            return existing;

        return new GameObject("TutorialManager").AddComponent<TutorialManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        G.Tutorial = this;
        _highlighter = gameObject.AddComponent<TutorialTargetHighlighter>();
        _uiBlocker = gameObject.AddComponent<TutorialUiBlocker>();
        TutorialSignals.Raised += OnTutorialSignal;
    }

    private void Start()
    {
        StartTutorial();
    }

    private void OnEnable()
    {
        SubscribeLocalization();
    }

    private void OnDisable()
    {
        UnsubscribeLocalization();
    }

    private void OnDestroy()
    {
        TutorialSignals.Raised -= OnTutorialSignal;
        UnsubscribeLocalization();
        ClearStarterEggOffer();

        if (_active && G.Ad != null)
            G.Ad.SetTutorialInterstitialSuppressed(false, PostTutorialInterstitialGraceSeconds);

        _uiBlocker?.End();
        _highlighter?.ClearTarget();
        if (G.Tutorial == this)
            G.Tutorial = null;
        if (Instance == this)
            Instance = null;
    }

    public void StartTutorial()
    {
        if (_active || _startRoutine != null)
            return;

        _startRoutine = StartCoroutine(StartWhenReady());
    }

    private IEnumerator StartWhenReady()
    {
        while (G.Save == null || !G.Save.IsReady || G.Currency == null || !G.Currency.IsInitialized || G.Player == null ||
               G.Inventory == null || !G.Inventory.IsInitialized || G.Storage == null)
        {
            yield return null;
        }

        _startRoutine = null;
        _state = G.Save.LoadTutorialState();
        if (_state == null)
            _state = TutorialSaveData.CreateNew();
        bool normalized = _state.Normalize(G.Save.GetTutorialProgress());
        _schedulerReady = true;
        if (normalized)
            SaveState();

        TryStartNextAvailable(emitSessionEvent: true);
    }

    private void Update()
    {
        if (!_schedulerReady || _state == null)
            return;

        if (!_active)
        {
            if (Time.unscaledTime >= _nextActivationScan)
            {
                _nextActivationScan = Time.unscaledTime + 1f;
                TryStartNextAvailable(emitSessionEvent: true);
            }
            return;
        }

        RefreshTargetIfNeeded();
#if UNITY_EDITOR
        if (_editorDebugFreezeProgress)
            return;
#endif
        EvaluateCurrentStep();
    }

    private bool TryStartNextAvailable(bool emitSessionEvent)
    {
        if (_active || _state == null)
            return _active;

        TutorialStepDefinition definition = null;
        TutorialTaskSaveData persistedActive = _state.GetTaskState(_state.activeStepId);
        if (persistedActive != null && persistedActive.status == TutorialTaskStatus.Active)
            definition = TutorialStepCatalog.Find(persistedActive.stableId);

        bool resumed = definition != null && persistedActive.startedUnix > 0;
        if (definition == null)
            definition = FindNextAvailableDefinition();

        if (definition == null)
        {
            if (_state.AreAllKnownStepsTerminal())
            {
                if (!G.Save.GetTutorialProgress())
                    G.Save.SaveTutorialProgress(true);
                _schedulerReady = false;
            }
            return false;
        }

        BeginDefinition(definition, emitSessionEvent, resumed);
        return true;
    }

    private TutorialStepDefinition FindNextAvailableDefinition()
    {
        TutorialStepDefinition best = null;
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialStepDefinition definition = TutorialStepCatalog.Steps[i];
            TutorialTaskSaveData state = _state.GetOrCreateTaskState(definition);
            if (state == null || state.IsTerminal || !ArePrerequisitesTerminal(definition) || !CanActivateDefinition(definition))
                continue;

            _state.SetAvailable(definition, UtcNowUnix());
            if (best == null || definition.priority < best.priority)
                best = definition;
        }

        return best;
    }

    private bool ArePrerequisitesTerminal(TutorialStepDefinition definition)
    {
        if (definition == null || definition.prerequisiteStableIds == null)
            return true;

        for (int i = 0; i < definition.prerequisiteStableIds.Length; i++)
        {
            string prerequisiteId = definition.prerequisiteStableIds[i];
            TutorialTaskSaveData prerequisite = _state.GetTaskState(prerequisiteId);
            if (prerequisite == null || !prerequisite.IsTerminal)
                return false;
        }

        return true;
    }

    private bool CanActivateDefinition(TutorialStepDefinition definition)
    {
        if (definition == null)
            return false;

        switch (definition.activationTrigger)
        {
            case TutorialActivationTrigger.PlayerReady:
                return G.Player != null;
            case TutorialActivationTrigger.LocalHomeReady:
                return ResolveLocalRoot() != null;
            case TutorialActivationTrigger.LocalConveyorReady:
                return FindLocalConveyor() != null;
            case TutorialActivationTrigger.StarterOfferReady:
                return FindLocalConveyor() != null && G.Storage != null && G.Storage.GetEgg(StarterEggId) != null;
            case TutorialActivationTrigger.StarterProgressItemPresent:
                return HasStarterProgressItem();
            case TutorialActivationTrigger.FreeLocalCellReady:
                return HasStarterProgressItem() && FindLocalFreeCell() != null;
            case TutorialActivationTrigger.StarterEggPlaced:
                return FindLocalEggCell() != null || FindLocalAnimalCell() != null;
            case TutorialActivationTrigger.StarterAnimalPresent:
                return FindLocalAnimalCell() != null;
            case TutorialActivationTrigger.CollectibleIncomeReady:
                return FindLocalAnimalCell(requireCollectibleIncome: true) != null;
            case TutorialActivationTrigger.ExpansionTargetReady:
                return FindNearestExpansionTarget() != null;
            case TutorialActivationTrigger.AlbumReady:
                return G.Album != null;
            case TutorialActivationTrigger.PrerequisitesTerminal:
            default:
                return true;
        }
    }

    private void BeginDefinition(TutorialStepDefinition definition, bool emitSessionEvent, bool resumed)
    {
        if (definition == null || _state == null)
            return;

        long now = UtcNowUnix();
        _currentTaskState = _state.Activate(definition, now);
        if (_currentTaskState == null)
            return;

        _currentDefinition = definition;
        if (_state.startedUnix <= 0)
            _state.startedUnix = now;

        CreateView();
        CloseConflictingWindows();
        _active = true;
        G.Ad?.SetTutorialInterstitialSuppressed(true);
        G.Save.SaveTutorialProgress(false);

        RunStartAction(definition.startAction);
        SaveState();

        if (emitSessionEvent)
            LogEvent(resumed || _state.startedUnix < now ? "tutorial_resumed" : "tutorial_started", BuildStepParameters());
        ActivateCurrentStep(logStepStarted: true);
    }

    private void EvaluateCurrentStep()
    {
        float activeFor = Time.realtimeSinceStartup - _stepActivatedRealtime;
        switch (_currentDefinition.completionTrigger)
        {
            case TutorialCompletionTrigger.MovementDistance:
                EvaluateMovement();
                break;

            case TutorialCompletionTrigger.ReachHintTarget:
                if (activeFor >= MinimumTargetStepDisplaySeconds && IsPlayerNear(_currentTarget, TargetReachDistance))
                    CompleteCurrentStep();
                break;

            case TutorialCompletionTrigger.StarterEggAcquired:
                if (HasStarterProgressItem())
                {
                    MarkStarterSideEffectGranted("acquire_starter_egg");
                    CompleteCurrentStep();
                }
                else if (activeFor >= 0.4f && Time.unscaledTime >= _nextStarterOfferAttempt)
                {
                    _nextStarterOfferAttempt = Time.unscaledTime + 1.5f;
                    EnsureStarterEggOffer();
                }
                break;

            case TutorialCompletionTrigger.StarterEggPlaced:
                if (FindLocalEggCell() != null || FindLocalAnimalCell() != null)
                    CompleteCurrentStep();
                break;

            case TutorialCompletionTrigger.StarterAnimalHatched:
                if (FindLocalAnimalCell() != null)
                {
                    MarkStarterSideEffectGranted("hatch_starter_egg");
                    CompleteCurrentStep();
                }
                break;

            case TutorialCompletionTrigger.StarterAnimalObserved:
                if (activeFor >= 1.4f && FindLocalAnimalCell() != null)
                    CompleteCurrentStep();
                break;

            case TutorialCompletionTrigger.FirstIncomeReady:
                FieldCell incomeCell = FindLocalAnimalCell(requireCollectibleIncome: true);
                if (incomeCell != null && incomeCell.CurrentBrainrot != null && incomeCell.CurrentBrainrot.CurrentIncome > 0d)
                    CompleteCurrentStep();
                break;

            case TutorialCompletionTrigger.AlbumRewardClaimed:
                if (HasClaimedStarterAlbumReward())
                    CompleteCurrentStep();
                break;
        }
    }

    private void RunStartAction(TutorialStartAction action)
    {
        if (action == TutorialStartAction.EnsureStarterEggOffer)
            EnsureStarterEggOffer();
    }

    private void EvaluateMovement()
    {
        if (G.Player == null)
            return;

        Vector3 position = G.Player.transform.position;
        Vector3 delta = position - _lastMovementPosition;
        delta.y = 0f;
        float frameDistance = delta.magnitude;
        if (frameDistance <= 2f)
            _movementDistance += frameDistance;
        _lastMovementPosition = position;

        if (_currentTaskState != null && _movementDistance - _lastSavedMovementProgress >= 0.5f)
        {
            _lastSavedMovementProgress = _movementDistance;
            _state.SetProgress(CurrentStableId, _movementDistance, string.Empty, UtcNowUnix());
            SaveState();
        }

        if (_movementDistance >= MovementDistanceRequired)
            CompleteCurrentStep();
    }

    private void ActivateCurrentStep(bool logStepStarted)
    {
        _state.Normalize(G.Save != null && G.Save.GetTutorialProgress());
        _stepActivatedRealtime = Time.realtimeSinceStartup;
        _movementDistance = _currentTaskState != null ? (float)_currentTaskState.progressValue : 0f;
        _lastSavedMovementProgress = _movementDistance;
        _lastMovementPosition = G.Player != null ? G.Player.transform.position : Vector3.zero;
        _nextStarterOfferAttempt = 0f;

        _uiBlocker?.SetAlbumAllowed(CurrentStep == TutorialStepId.ClaimAlbumReward);
        RefreshView();
        RefreshTarget(force: true);

        if (logStepStarted)
            LogEvent("tutorial_step_started", BuildStepParameters());
    }

    private void CompleteCurrentStep()
    {
        if (!_active || _state == null || _currentDefinition == null || _processingTransition)
            return;

        _processingTransition = true;
        if (CurrentStep == TutorialStepId.AcquireStarterEgg)
            ClearStarterEggOffer();

        CompleteCurrentProgress();
        _state.MarkTerminal(CurrentStableId, wasSkipped: false, UtcNowUnix());
        SaveState();
        GrantCurrentCompletionReward();
        Dictionary<string, object> finalParameters = BuildStepParameters();
        LogEvent("tutorial_step_completed", finalParameters);
        AdvanceAfterTerminal(finalParameters, "tutorial_completed");
        _processingTransition = false;
    }

    private void AdvanceAfterTerminal(Dictionary<string, object> finalParameters, string terminalEventName)
    {
        _currentDefinition = null;
        _currentTaskState = null;
        _active = false;

        TutorialStepDefinition next = FindNextAvailableDefinition();
        if (next != null)
        {
            BeginDefinition(next, emitSessionEvent: false, resumed: false);
            return;
        }

        FinishActiveSession(terminalEventName, finalParameters);
    }

    private void FinishActiveSession(string eventName, Dictionary<string, object> parameters)
    {
        ClearStarterEggOffer();
        _state.Normalize(G.Save != null && G.Save.GetTutorialProgress());
        SaveState();
        bool allKnownTerminal = _state.AreAllKnownStepsTerminal();
        if (allKnownTerminal)
        {
            G.Save.SaveTutorialProgress(true);
            _schedulerReady = false;
        }

        if (allKnownTerminal && !string.IsNullOrWhiteSpace(eventName))
            LogEvent(eventName, parameters);

        _active = false;
        G.Ad?.SetTutorialInterstitialSuppressed(false, PostTutorialInterstitialGraceSeconds);
        _uiBlocker?.End();
        _highlighter?.ClearTarget();
        if (_view != null)
            Destroy(_view.gameObject);
        _view = null;
        _currentDefinition = null;
        _currentTaskState = null;
        _nextActivationScan = Time.unscaledTime + 1f;
    }

#if UNITY_EDITOR
    public void EditorDebugSetStep(int stepIndex)
    {
        if (!_active || _state == null)
            return;

        int clamped = Mathf.Clamp(stepIndex, 0, TutorialStepCatalog.Steps.Length - 1);
        _currentDefinition = TutorialStepCatalog.Steps[clamped];
        _currentTaskState = _state.Activate(_currentDefinition, UtcNowUnix());
        _editorDebugFreezeProgress = true;
        ActivateCurrentStep(logStepStarted: false);
    }

    public void EditorDebugResumeProgress()
    {
        _editorDebugFreezeProgress = false;
    }
#endif

    private void MarkStarterSideEffectGranted(string stableId, bool saveImmediately = true)
    {
        if (_state == null)
            return;

        if (string.Equals(stableId, "acquire_starter_egg", StringComparison.Ordinal))
            _state.starterEggGranted = true;
        else if (string.Equals(stableId, "hatch_starter_egg", StringComparison.Ordinal))
            _state.starterAnimalGranted = true;

        TutorialTaskSaveData task = _state.GetTaskState(stableId);
        if (task != null)
        {
            task.rewardGranted = true;
            task.updatedUnix = UtcNowUnix();
        }

        if (saveImmediately)
            SaveState();
    }

    private void CompleteCurrentProgress()
    {
        if (_state == null || _currentDefinition == null || _currentDefinition.progressTarget <= 0d)
            return;
        _state.SetProgress(
            _currentDefinition.stableId,
            _currentDefinition.progressTarget,
            string.Empty,
            UtcNowUnix());
    }

    private bool GrantCurrentCompletionReward()
    {
        if (_state == null || _currentDefinition == null || _currentTaskState == null ||
            _currentTaskState.completionRewardGranted)
        {
            return false;
        }

        int amount = Mathf.Clamp(_currentDefinition.completionRewardGems, 1, 3);
        if (G.Currency == null)
        {
            Debug.LogError($"[Tutorial] Cannot grant {amount} gems for '{CurrentStableId}': CurrencyManager is unavailable.");
            return false;
        }

        // Persist the idempotence flag before mutating the currency balance. This
        // prevents duplicate rewards if completion is delivered more than once.
        _currentTaskState.completionRewardGranted = true;
        _currentTaskState.updatedUnix = UtcNowUnix();
        SaveState();

        try
        {
            G.Currency.AddCurrency(CurrencyType.Gems, amount);
            return true;
        }
        catch (Exception ex)
        {
            _currentTaskState.completionRewardGranted = false;
            SaveState();
            Debug.LogError($"[Tutorial] Failed to grant completion reward for '{CurrentStableId}': {ex.Message}");
            return false;
        }
    }

    private void OnDonePressed()
    {
        if (CurrentStep == TutorialStepId.ContinueIndependently)
            CompleteCurrentStep();
    }

    private void OnTutorialSignal(TutorialSignal signal)
    {
        if (!_schedulerReady)
            return;
        if (!_active)
        {
            _nextActivationScan = 0f;
            return;
        }
        if (_processingTransition || !IsSignalFromLocalGameplay(signal))
            return;

        switch (_currentDefinition.completionTrigger)
        {
            case TutorialCompletionTrigger.StarterEggAcquired:
                if (signal.Type == TutorialSignalType.ItemAcquired && signal.ItemType == Item.Egg)
                {
                    MarkStarterSideEffectGranted("acquire_starter_egg");
                    CompleteCurrentStep();
                }
                break;

            case TutorialCompletionTrigger.StarterEggPlaced:
                if (signal.Type == TutorialSignalType.EggPlaced)
                    CompleteCurrentStep();
                break;

            case TutorialCompletionTrigger.StarterAnimalHatched:
                if (signal.Type == TutorialSignalType.AnimalHatched)
                {
                    MarkStarterSideEffectGranted("hatch_starter_egg");
                    CompleteCurrentStep();
                }
                break;

            case TutorialCompletionTrigger.FirstIncomeReady:
                if (signal.Type == TutorialSignalType.IncomeReady)
                    CompleteCurrentStep();
                break;

            case TutorialCompletionTrigger.FirstIncomeCollected:
                if (signal.Type == TutorialSignalType.IncomeCollected && signal.Value > 0d)
                    CompleteCurrentStep();
                break;

            case TutorialCompletionTrigger.FirstExpansionMade:
                if ((signal.Type == TutorialSignalType.ItemAcquired && signal.ItemType == Item.Egg) ||
                    signal.Type == TutorialSignalType.FieldUnlocked ||
                    signal.Type == TutorialSignalType.ConveyorUpgraded)
                {
                    CompleteCurrentStep();
                }
                break;

            case TutorialCompletionTrigger.AlbumRewardClaimed:
                if (signal.Type == TutorialSignalType.AlbumRewardClaimed)
                    CompleteCurrentStep();
                break;
        }
    }

    public int GetHatchDurationSeconds(Egg egg, int originalDurationSeconds)
    {
        if (!_active || _state == null || egg == null || _state.starterAnimalGranted)
            return originalDurationSeconds;
        TutorialTaskSaveData hatchState = _state.GetTaskState("hatch_starter_egg");
        if (hatchState != null && hatchState.IsTerminal)
            return originalDurationSeconds;
        if (!string.Equals(egg.Name, StarterEggId, StringComparison.OrdinalIgnoreCase))
            return originalDurationSeconds;

        return Mathf.Min(Mathf.Max(1, originalDurationSeconds), StarterHatchDurationSeconds);
    }

    public bool TryGetGuaranteedStarterAnimal(Egg egg, out Brainrot animal)
    {
        animal = null;
        if (!_active || _state == null || _state.starterAnimalGranted || egg == null)
            return false;
        TutorialTaskSaveData hatchState = _state.GetTaskState("hatch_starter_egg");
        if (hatchState != null && hatchState.IsTerminal)
            return false;
        if (!string.Equals(egg.Name, StarterEggId, StringComparison.OrdinalIgnoreCase))
            return false;

        animal = G.Storage != null ? G.Storage.GetPet(StarterAnimalId) : null;
        return animal != null && Egg.IsAnimalDrop(animal);
    }

    public bool TryPrepareStarterEggPurchase(Egg egg)
    {
        if (!_active || _state == null || CurrentStep != TutorialStepId.AcquireStarterEgg ||
            egg == null || !string.Equals(egg.Name, StarterEggId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Conveyor conveyor = FindLocalConveyor();
        if (conveyor == null || !conveyor.IsTrackedEgg(egg))
            return false;

        _starterOfferEgg = egg;
        egg.SetTutorialFreePurchase(true);
        BrainrotDinamicData data = egg.Data.DinamicData;
        data.ElementType = ElementType.NoElement;
        data.WeightMultiplier = 1f;
        data.ResultIncome = 0d;
        egg.SetData(data);
        return true;
    }

    private void EnsureStarterEggOffer()
    {
        if (_state.starterEggGranted || HasStarterProgressItem())
            return;

        Egg existingOffer = FindStarterEggOnConveyor();
        if (existingOffer != null)
        {
            SetStarterEggOffer(existingOffer);
            return;
        }

        Egg prefab = G.Storage.GetEgg(StarterEggId);
        if (prefab == null)
        {
            if (!_starterOfferErrorLogged)
            {
                _starterOfferErrorLogged = true;
                Debug.LogError($"[Tutorial] Starter egg '{StarterEggId}' was not found in ItemPrefabStorage.");
            }
            return;
        }

        Conveyor conveyor = FindLocalConveyor();
        Egg spawnedOffer = conveyor != null ? conveyor.SpawnTutorialEgg(prefab) : null;
        if (spawnedOffer != null)
        {
            SetStarterEggOffer(spawnedOffer);
            return;
        }

        if (!_starterOfferErrorLogged)
        {
            _starterOfferErrorLogged = true;
            Debug.LogError("[Tutorial] Local conveyor cannot spawn the starter egg offer.");
        }
    }

    private void SetStarterEggOffer(Egg egg)
    {
        if (_starterOfferEgg != null && _starterOfferEgg != egg)
            _starterOfferEgg.SetTutorialFreePurchase(false);
        _starterOfferEgg = egg;
        _starterOfferEgg.SetTutorialFreePurchase(true);
    }

    private void ClearStarterEggOffer()
    {
        if (_starterOfferEgg != null)
            _starterOfferEgg.SetTutorialFreePurchase(false);
        _starterOfferEgg = null;
    }

    private bool HasStarterProgressItem()
    {
        if (G.Inventory != null)
        {
            var eggs = G.Inventory.GetItems(Item.Egg);
            if (eggs != null && eggs.Count > 0)
                return true;
        }

        return FindLocalEggCell() != null || FindLocalAnimalCell() != null;
    }

    private bool HasClaimedStarterAlbumReward()
    {
        if (G.Album == null)
            return false;

        FieldCell cell = FindLocalAnimalCell();
        Brainrot animal = cell != null ? cell.CurrentBrainrot : null;
        if (animal == null)
            return false;

        return G.Album.IsRewardClaimed(AlbumEntityType.Animal, animal.Name, animal.DinamicData.ElementType) ||
               G.Album.IsRewardClaimed(AlbumEntityType.Animal, animal.Name);
    }

    private bool IsSignalFromLocalGameplay(TutorialSignal signal)
    {
        if (signal.Context == null)
            return true;

        Component component = signal.Context as Component;
        if (component == null && signal.Context is GameObject go)
            component = go.transform;
        if (component == null)
            return true;

        FieldCell cell = component.GetComponentInParent<FieldCell>();
        if (cell != null)
            return !cell.IsRemoteMode && IsWithinLocalRoot(cell.transform);

        Field field = component.GetComponentInParent<Field>();
        if (field != null)
            return !field.IsRemoteMode && IsWithinLocalRoot(field.transform);

        Conveyor conveyor = component.GetComponentInParent<Conveyor>();
        if (conveyor != null)
            return !conveyor.IsRemoteMode && IsWithinLocalRoot(conveyor.transform);

        return true;
    }

    private void RefreshTargetIfNeeded()
    {
        if (Time.unscaledTime < _nextTargetRefresh)
            return;
        RefreshTarget(force: false);
    }

    private void RefreshTarget(bool force)
    {
        if (!force && Time.unscaledTime < _nextTargetRefresh)
            return;

        _nextTargetRefresh = Time.unscaledTime + 0.45f;
        Transform target = ResolveTargetForCurrentStep();
        if (_currentTarget == target)
            return;

        _currentTarget = target;
        _view?.SetWorldTarget(_currentTarget);
        _highlighter?.SetTarget(_currentTarget);
    }

    private Transform ResolveTargetForCurrentStep()
    {
        if (_currentDefinition == null)
            return null;

        switch (_currentDefinition.hintTarget)
        {
            case TutorialHintTarget.LocalHome:
                return GetRemoteBases()?.GetLocalSlotEntryPoint() ?? ResolveLocalRoot();

            case TutorialHintTarget.LocalConveyor:
                return FindLocalConveyor()?.transform;

            case TutorialHintTarget.StarterEggOffer:
                return FindStarterEggOnConveyor()?.transform ?? FindLocalConveyor()?.transform;

            case TutorialHintTarget.ExpansionTarget:
                return FindNearestExpansionTarget();

            case TutorialHintTarget.FreeLocalCell:
                return FindLocalFreeCell()?.transform;

            case TutorialHintTarget.LocalEggCell:
                return FindLocalEggCell()?.transform;

            case TutorialHintTarget.LocalAnimalCell:
                return FindLocalAnimalCell()?.transform;

            case TutorialHintTarget.CollectibleIncomeCell:
                return (FindLocalAnimalCell(requireCollectibleIncome: true) ?? FindLocalAnimalCell())?.transform;

            case TutorialHintTarget.AlbumTarget:
                return FindAlbumTarget();

            case TutorialHintTarget.None:
            default:
                return null;
        }
    }

    private Transform ResolveLocalRoot()
    {
        return GetRemoteBases()?.GetLocalSlotRoot();
    }

    private RemoteBasesApplier GetRemoteBases()
    {
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        return _remoteBases;
    }

    private Conveyor FindLocalConveyor()
    {
        Transform root = ResolveLocalRoot();
        if (root == null)
            return null;

        Conveyor[] conveyors = root.GetComponentsInChildren<Conveyor>(true);
        for (int i = 0; i < conveyors.Length; i++)
        {
            if (conveyors[i] != null && !conveyors[i].IsRemoteMode)
                return conveyors[i];
        }

        return null;
    }

    private FieldCell FindLocalFreeCell()
    {
        FieldCell[] cells = GetLocalCells();
        FieldCell best = null;
        float bestDistance = float.MaxValue;
        Vector3 origin = G.Player != null ? G.Player.transform.position : Vector3.zero;
        for (int i = 0; i < cells.Length; i++)
        {
            FieldCell cell = cells[i];
            if (cell == null || !cell.gameObject.activeInHierarchy || cell.IsRemoteMode || !cell.IsFree)
                continue;

            float distance = (cell.transform.position - origin).sqrMagnitude;
            if (distance >= bestDistance)
                continue;
            best = cell;
            bestDistance = distance;
        }

        return best;
    }

    private FieldCell FindLocalEggCell()
    {
        FieldCell[] cells = GetLocalCells();
        FieldCell nearest = null;
        float nearestDistance = float.MaxValue;
        Vector3 origin = G.Player != null ? G.Player.transform.position : Vector3.zero;
        for (int i = 0; i < cells.Length; i++)
        {
            FieldCell cell = cells[i];
            if (cell == null || cell.IsRemoteMode || cell.CurrentEgg == null)
                continue;
            float distance = (cell.transform.position - origin).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearest = cell;
            nearestDistance = distance;
        }

        return nearest;
    }

    private FieldCell FindLocalAnimalCell(bool requireCollectibleIncome = false)
    {
        FieldCell[] cells = GetLocalCells();
        FieldCell nearest = null;
        float nearestDistance = float.MaxValue;
        Vector3 origin = G.Player != null ? G.Player.transform.position : Vector3.zero;
        for (int i = 0; i < cells.Length; i++)
        {
            FieldCell cell = cells[i];
            if (cell == null || cell.IsRemoteMode || cell.CurrentBrainrot == null ||
                (requireCollectibleIncome && !cell.CurrentBrainrot.HasCollectibleIncome))
                continue;
            float distance = (cell.transform.position - origin).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearest = cell;
            nearestDistance = distance;
        }

        return nearest;
    }

    private Egg FindStarterEggOnConveyor()
    {
        Conveyor conveyor = FindLocalConveyor();
        Vector3 origin = G.Player != null ? G.Player.transform.position : Vector3.zero;
        return conveyor != null ? conveyor.FindNearestAvailableEgg(origin, StarterEggId) : null;
    }

    private Transform FindNearestExpansionTarget()
    {
        Vector3 origin = G.Player != null ? G.Player.transform.position : Vector3.zero;
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        Conveyor conveyor = FindLocalConveyor();
        if (conveyor != null)
        {
            Egg egg = conveyor.FindNearestAvailableEgg(origin);
            ConsiderNearestTarget(egg != null ? egg.transform : null, origin, ref nearest, ref nearestDistance);
            ConsiderNearestTarget(conveyor.transform, origin, ref nearest, ref nearestDistance);
        }

        Transform root = ResolveLocalRoot();
        if (root != null)
        {
            Field[] fields = root.GetComponentsInChildren<Field>(true);
            for (int i = 0; i < fields.Length; i++)
            {
                Field field = fields[i];
                if (field == null || field.IsRemoteMode || field.IsUnblocked || !field.gameObject.activeInHierarchy)
                    continue;
                ConsiderNearestTarget(field.transform, origin, ref nearest, ref nearestDistance);
            }
        }

        return nearest;
    }

    private Transform FindAlbumTarget()
    {
        AlbumScreenController[] albums = FindObjectsByType<AlbumScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < albums.Length; i++)
        {
            AlbumScreenController album = albums[i];
            if (album == null || !album.IsOpen)
                continue;

            Button[] albumButtons = album.GetComponentsInChildren<Button>(true);
            for (int j = 0; j < albumButtons.Length; j++)
            {
                Button button = albumButtons[j];
                if (button != null && button.gameObject.activeInHierarchy && button.interactable &&
                    BuildHierarchyName(button.transform).IndexOf("reward", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return button.transform;
                }
            }

            AlbumEntryView[] cards = album.GetComponentsInChildren<AlbumEntryView>(true);
            AlbumEntryView unlockedFallback = null;
            for (int j = 0; j < cards.Length; j++)
            {
                AlbumEntryView card = cards[j];
                if (card == null || !card.gameObject.activeInHierarchy || !card.IsUnlocked)
                    continue;
                if (card.HasMention)
                    return card.transform;
                if (unlockedFallback == null)
                    unlockedFallback = card;
            }

            if (unlockedFallback != null)
                return unlockedFallback.transform;

            return album.transform;
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.gameObject.activeInHierarchy && button.interactable &&
                string.Equals(button.name, "AlbumButton", StringComparison.OrdinalIgnoreCase))
            {
                return button.transform;
            }
        }

        return null;
    }

    private static void ConsiderNearestTarget(
        Transform candidate,
        Vector3 origin,
        ref Transform nearest,
        ref float nearestDistance)
    {
        if (candidate == null)
            return;
        float distance = (candidate.position - origin).sqrMagnitude;
        if (distance >= nearestDistance)
            return;
        nearest = candidate;
        nearestDistance = distance;
    }

    private static string BuildHierarchyName(Transform current)
    {
        string result = string.Empty;
        int depth = 0;
        while (current != null && depth++ < 8)
        {
            result += "/" + current.name;
            current = current.parent;
        }

        return result;
    }

    private FieldCell[] GetLocalCells()
    {
        Transform root = ResolveLocalRoot();
        return root != null ? root.GetComponentsInChildren<FieldCell>(true) : Array.Empty<FieldCell>();
    }

    private bool IsWithinLocalRoot(Transform context)
    {
        Transform root = ResolveLocalRoot();
        return root == null || context == root || context.IsChildOf(root);
    }

    private static bool IsPlayerNear(Transform target, float distance)
    {
        if (target == null || G.Player == null)
            return false;

        Vector3 delta = target.position - G.Player.transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= distance * distance;
    }

    private void CreateView()
    {
        if (_view != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(ViewResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[Tutorial] View prefab was not found at Resources/{ViewResourcePath}.prefab.");
            return;
        }

        Canvas canvas = FindBestScreenCanvas();
        GameObject viewObject = canvas != null
            ? Instantiate(prefab, canvas.transform, false)
            : Instantiate(prefab);
        viewObject.name = "TutorialView";
        _view = viewObject.GetComponent<TutorialView>();
        if (_view == null)
            _view = viewObject.AddComponent<TutorialView>();
        _view.Initialize();
        _view.DonePressed += OnDonePressed;
        _uiBlocker?.Begin(_view);
    }

    private static Canvas FindBestScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas best = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || !canvas.isActiveAndEnabled)
                continue;
            if (canvas.name.StartsWith("GameCanvas", StringComparison.Ordinal))
                return canvas;
            if (best == null || canvas.sortingOrder > best.sortingOrder)
                best = canvas;
        }

        return best;
    }

    private void RefreshView()
    {
        if (_view == null || _state == null || _currentDefinition == null)
            return;

        TutorialStepDefinition step = _currentDefinition;
        bool touch = G.Control != null && G.Control.UseTouchControl;
        string message = touch
            ? L(step.touchTextKey, step.touchFallback)
            : L(step.desktopTextKey, step.desktopFallback);
        string progress = LocalizationUtils.Format(
            "UI/Tutorial/Progress",
            "Tutorial {0}/{1}",
            Mathf.Min(TutorialStepCatalog.Steps.Length, _state.CountTerminalKnownSteps() + 1),
            TutorialStepCatalog.Steps.Length);
        bool final = CurrentStep == TutorialStepId.ContinueIndependently;
        string reward = LocalizationUtils.Format(
            "UI/Tutorial/Reward",
            "Reward: +{0}",
            step.completionRewardGems);
        Sprite rewardIcon = G.Currency != null
            ? G.Currency.GetCurrencyIcon(CurrencyType.Gems)
            : null;

        _view.SetStep(progress, message, reward, rewardIcon, L("UI/Tutorial/Done", "Done"), final);
        _view.SetWorldTarget(_currentTarget);
    }

    private void CloseConflictingWindows()
    {
        AlbumScreenController[] albums = FindObjectsByType<AlbumScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < albums.Length; i++)
        {
            if (albums[i] != null && albums[i].IsOpen)
                albums[i].Close();
        }

        if (G.SpecialShop != null && G.SpecialShop.Opened)
            G.SpecialShop.Close();
    }

    private void SaveState()
    {
        if (_state == null || G.Save == null)
            return;
        _state.Normalize(G.Save.GetTutorialProgress());
        G.Save.SaveTutorialState(_state);
    }

    private Dictionary<string, object> BuildStepParameters()
    {
        TutorialStepDefinition step = _currentDefinition ?? TutorialStepCatalog.Steps[CurrentStepIndex];
        long now = UtcNowUnix();
        long stepStart = _currentTaskState != null && _currentTaskState.startedUnix > 0
            ? _currentTaskState.startedUnix
            : now;
        return new Dictionary<string, object>
        {
            ["step_id"] = step.stableId,
            ["step_index"] = CurrentStepIndex,
            ["elapsed_sec"] = Math.Max(0L, now - stepStart),
            ["progress_value"] = _currentTaskState != null ? _currentTaskState.progressValue : 0d,
            ["progress_target"] = step.progressTarget,
            ["activation_trigger"] = step.activationTrigger.ToString(),
            ["completion_trigger"] = step.completionTrigger.ToString(),
            ["completion_reward_gems"] = step.completionRewardGems,
            ["completion_reward_granted"] = _currentTaskState != null && _currentTaskState.completionRewardGranted,
            ["input_mode"] = G.Control != null && G.Control.UseTouchControl ? "touch" : "desktop",
            ["online_mode"] = LobbyClient.Instance != null && LobbyClient.Instance.IsOnline ? "online" : "offline"
        };
    }

    private static void LogEvent(string eventName, Dictionary<string, object> parameters)
    {
        AnalyticsManager.Instance.LogEvent(eventName, parameters);
    }

    private void SubscribeLocalization()
    {
        LocalizationManager manager = LocalizationManager.Instance;
        if (manager != null)
        {
            manager.OnLanguageChanged -= OnLanguageChanged;
            manager.OnLanguageChanged += OnLanguageChanged;
        }
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLocalization()
    {
        LocalizationManager manager = LocalizationManager.Instance;
        if (manager != null)
            manager.OnLanguageChanged -= OnLanguageChanged;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string _)
    {
        RefreshView();
    }

    private static string L(string key, string fallback)
    {
        return LocalizationUtils.T(key, fallback);
    }

    private static long UtcNowUnix()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

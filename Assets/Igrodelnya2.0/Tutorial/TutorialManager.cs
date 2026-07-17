using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MirraGames.SDK;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialManager : MonoBehaviour
{
    private const string ViewResourcePath = "Tutorial/TutorialView";
    private const string StarterEggId = "egg1";
    private const string StarterAnimalId = "Capybara";
    private const float MovementDistanceRequired = 0.25f;
    private const float ContextRefreshSeconds = 0.2f;
    private const float ActivationRefreshSeconds = 0.5f;
    private const float MinimumAutoCompletionDisplaySeconds = 0.8f;
    private const float CompletionPresentationSeconds = 1.35f;
    private const float LocalHomeRadius = 45f;
    private const float PostTutorialInterstitialGraceSeconds = 45f;

    public static TutorialManager Instance { get; private set; }

    private TutorialSaveData _state;
    private TutorialStepDefinition _currentDefinition;
    private TutorialTaskSaveData _currentTaskState;
    private TutorialView _view;
    private TutorialTargetHighlighter _highlighter;
    private RemoteBasesApplier _remoteBases;
    private Transform _primaryTarget;
    private Transform _secondaryTarget;
    private Vector3 _lastMovementPosition;
    private float _movementDistance;
    private float _stepActivatedRealtime;
    private float _nextContextRefresh;
    private float _nextActivationRefresh;
    private float _nextAffordableEggAttempt;
    private bool _active;
    private bool _schedulerReady;
    private bool _processingTransition;
    private bool _awaitingRewardClaim;
    private bool _establishedPlayerAtSessionStart;
    private bool _sessionEventLogged;
    private Coroutine _startRoutine;

#if UNITY_EDITOR
    private bool _editorDebugFreezeProgress;
#endif

    public bool IsTutorialActive => _active;
    public bool AllowsAlbum => !_active || CurrentStep == TutorialStepId.ClaimFirstAlbumRewards;
    public bool IsFreeEggSpeedupAvailable => _state != null && !_state.freeEggSpeedupUsed;
    public Transform CurrentTarget => _primaryTarget;
    public TutorialStepId CurrentStep => _currentDefinition != null
        ? _currentDefinition.id
        : TutorialStepId.LearnMovement;
    public string CurrentStableId => _currentDefinition != null ? _currentDefinition.stableId : string.Empty;

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
        DontDestroyOnLoad(gameObject);
        _highlighter = gameObject.AddComponent<TutorialTargetHighlighter>();
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
        if (_view != null)
            _view.RewardClaimPressed -= OnRewardClaimPressed;
        if (_active)
            G.Ad?.SetTutorialInterstitialSuppressed(false, PostTutorialInterstitialGraceSeconds);
        if (G.Tutorial == this)
            G.Tutorial = null;
        if (Instance == this)
            Instance = null;
    }

    public void StartTutorial()
    {
        if (_startRoutine != null)
            StopCoroutine(_startRoutine);
        _startRoutine = StartCoroutine(StartWhenReady());
    }

    private IEnumerator StartWhenReady()
    {
        while (G.Save == null || !G.Save.IsReady || G.Currency == null || !G.Currency.IsInitialized ||
               G.Inventory == null || !G.Inventory.IsInitialized || G.QuickAccess == null || G.Player == null)
        {
            yield return null;
        }

        _establishedPlayerAtSessionStart = !G.Save.IsNewPlayer;
        _state = G.Save.LoadTutorialState() ?? TutorialSaveData.CreateNew();
        bool normalized = _state.Normalize(G.Save.GetTutorialProgress());
        CaptureKnownTutorialEntities();
        if (normalized)
            SaveState();

        CreateView();
        _schedulerReady = true;
        _startRoutine = null;

        TutorialTaskSaveData persisted = _state.GetTaskState(_state.activeStepId);
        TutorialStepDefinition persistedDefinition = persisted != null && persisted.status == TutorialTaskStatus.Active
            ? TutorialStepCatalog.Find(persisted.stableId)
            : null;
        if (persistedDefinition != null && ArePrerequisitesTerminal(persistedDefinition))
            BeginDefinition(persistedDefinition, resumed: true);
        else
            TryStartNextAvailable();
    }

    private void Update()
    {
        if (!_schedulerReady || _state == null || _processingTransition)
            return;

#if UNITY_EDITOR
        if (_editorDebugFreezeProgress)
            return;
#endif

        if (!_active)
        {
            if (Time.unscaledTime >= _nextActivationRefresh)
            {
                _nextActivationRefresh = Time.unscaledTime + ActivationRefreshSeconds;
                TryStartNextAvailable();
            }
            return;
        }

        if (_awaitingRewardClaim)
            return;

        if (Time.unscaledTime >= _nextContextRefresh)
        {
            _nextContextRefresh = Time.unscaledTime + ContextRefreshSeconds;
            RefreshContext();
            EvaluateCurrentStep();
        }
    }

    private bool TryStartNextAvailable()
    {
        TutorialStepDefinition definition = FindNextAvailableDefinition();
        if (definition != null)
        {
            BeginDefinition(definition, resumed: false);
            return true;
        }

        PauseSessionUntilContext();
        return false;
    }

    private TutorialStepDefinition FindNextAvailableDefinition()
    {
        TutorialStepDefinition best = null;
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialStepDefinition definition = TutorialStepCatalog.Steps[i];
            TutorialTaskSaveData task = _state.GetOrCreateTaskState(definition);
            if (task == null || task.IsTerminal || !ArePrerequisitesTerminal(definition) || !CanActivateDefinition(definition))
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
            TutorialTaskSaveData prerequisite = _state.GetTaskState(definition.prerequisiteStableIds[i]);
            if (prerequisite == null || !prerequisite.IsTerminal)
                return false;
        }

        return true;
    }

    private bool CanActivateDefinition(TutorialStepDefinition definition)
    {
        switch (definition.activationTrigger)
        {
            case TutorialActivationTrigger.PlayerReady:
                return G.Player != null;
            case TutorialActivationTrigger.AffordableEggAvailable:
                return FindLocalConveyor() != null;
            case TutorialActivationTrigger.OwnedEggAvailable:
                return ResolveLocalRoot() != null || FindLocalConveyor() != null;
            case TutorialActivationTrigger.MaturingEggAvailable:
                return !_state.freeEggSpeedupUsed && FindLocalEggCell(EggStatus.Maturing) != null;
            case TutorialActivationTrigger.ReadyEggAvailable:
                return FindLocalEggCell(EggStatus.ReadyToHatch) != null || HasHatchedOrLaterProgress();
            case TutorialActivationTrigger.AlbumRewardsReady:
                return G.Album != null && HasHatchedOrLaterProgress();
            case TutorialActivationTrigger.BigPetReady:
                return FindLocalBigPet() != null;
            case TutorialActivationTrigger.FoodLessonReady:
                return FindLocalBigPet() != null && FindFoodShop() != null;
            case TutorialActivationTrigger.TerritoryReady:
                return FindCheapestLockedField() != null || HasUnlockedExpansion();
            case TutorialActivationTrigger.ConveyorUpgradeReady:
                return FindLocalConveyor() != null;
            default:
                return false;
        }
    }

    private void BeginDefinition(TutorialStepDefinition definition, bool resumed)
    {
        if (definition == null)
            return;

        _currentDefinition = definition;
        _currentTaskState = _state.Activate(definition, UtcNowUnix());
        if (_currentTaskState == null)
            return;

        _active = true;
        _processingTransition = false;
        _awaitingRewardClaim = false;
        _stepActivatedRealtime = Time.unscaledTime;
        _nextContextRefresh = 0f;
        _movementDistance = Math.Max(0d, _currentTaskState.progressValue) > float.MaxValue
            ? 0f
            : (float)Math.Max(0d, _currentTaskState.progressValue);
        _lastMovementPosition = G.Player != null ? G.Player.transform.position : Vector3.zero;
        G.Ad?.SetTutorialInterstitialSuppressed(true);
        G.Save.SaveTutorialProgress(false);
        if (_view != null)
            _view.gameObject.SetActive(true);

        if (!_currentTaskState.objectiveCompleted)
        {
            RunStartAction(definition.startAction);
            RefreshContext();
        }
        else
        {
            PresentClaimableCompletion();
        }
        SaveState();

        if (!_sessionEventLogged)
        {
            LogEvent(resumed ? "tutorial_resumed" : "tutorial_started", BuildStepParameters());
            _sessionEventLogged = true;
        }

        if (!resumed)
            LogEvent("tutorial_step_started", BuildStepParameters());
    }

    private void RunStartAction(TutorialStartAction action)
    {
        if (action == TutorialStartAction.EnsureAffordableEgg)
            EnsureAffordableEgg();
        if (CurrentStep == TutorialStepId.FeedBigPet)
            EnsureTutorialFoodAvailable();
    }

    private void EvaluateCurrentStep()
    {
        if (_currentDefinition == null || _currentTaskState == null)
            return;

        if (CurrentStep == TutorialStepId.LearnMovement)
            EvaluateMovement();

        if (CurrentStep == TutorialStepId.UseFreeEggSpeedup && !_state.freeEggSpeedupUsed &&
            FindLocalEggCell(EggStatus.Maturing) == null)
        {
            SuspendCurrentStep();
            return;
        }

        if (Time.unscaledTime - _stepActivatedRealtime < MinimumAutoCompletionDisplaySeconds)
            return;

        if (IsCurrentDefinitionAlreadySatisfied())
            CompleteCurrentStep(autoCompleted: true);
    }

    private void EvaluateMovement()
    {
        if (G.Player == null)
            return;

        Vector3 current = G.Player.transform.position;
        Vector3 delta = current - _lastMovementPosition;
        delta.y = 0f;
        if (delta.magnitude < 2f)
            _movementDistance += delta.magnitude;
        _lastMovementPosition = current;
        _state.SetProgress(CurrentStableId, _movementDistance, string.Empty, UtcNowUnix());
    }

    private bool IsCurrentDefinitionAlreadySatisfied()
    {
        switch (_currentDefinition.completionTrigger)
        {
            case TutorialCompletionTrigger.MovementStarted:
                return _movementDistance >= MovementDistanceRequired ||
                       (_establishedPlayerAtSessionStart && HasAnyEstablishedProgress());
            case TutorialCompletionTrigger.EggPurchased:
                return HasOwnedEggOrLaterProgress();
            case TutorialCompletionTrigger.EggPlaced:
                return HasPlacedEggOrLaterProgress();
            case TutorialCompletionTrigger.EggSpeedupUsed:
                return _state.freeEggSpeedupUsed;
            case TutorialCompletionTrigger.EggHatched:
                return HasHatchedOrLaterProgress();
            case TutorialCompletionTrigger.AlbumRewardsClaimed:
                return AreTutorialAlbumRewardsClaimed();
            case TutorialCompletionTrigger.BigPetPurchased:
                return FindLocalBigPet()?.IsPurchased == true;
            case TutorialCompletionTrigger.BigPetFed:
                return HasFedBigPet();
            case TutorialCompletionTrigger.TerritoryUnlocked:
                return HasUnlockedExpansion();
            case TutorialCompletionTrigger.ConveyorUpgraded:
                return FindLocalConveyor()?.HasAnyUpgrade == true;
            default:
                return false;
        }
    }

    private void CompleteCurrentStep(bool autoCompleted)
    {
        if (_processingTransition || _awaitingRewardClaim || _currentDefinition == null || _currentTaskState == null)
            return;

        _awaitingRewardClaim = true;
        _currentTaskState.objectiveCompleted = true;
        _currentTaskState.objectiveAutoCompleted = autoCompleted;
        _currentTaskState.updatedUnix = UtcNowUnix();
        SaveState();
        PresentClaimableCompletion();
    }

    private void PresentClaimableCompletion()
    {
        if (_currentDefinition == null || _currentTaskState == null)
            return;

        _awaitingRewardClaim = true;
        int reward = Math.Max(0, _currentDefinition.completionRewardGems);
        SetTargets(null, null, null);
        _view?.ShowCompleted(
            L("UI/Tutorial/Completed", "TASK COMPLETE!"),
            reward > 0
                ? FormatLocalized("UI/Tutorial/ClaimReward", "Claim +{0}", reward)
                : L("UI/Tutorial/Continue", "Continue"),
            reward > 0 ? G.Currency?.GetCurrencyIcon(CurrencyType.Gems) : null);
    }

    private void OnRewardClaimPressed()
    {
        if (!_awaitingRewardClaim || _processingTransition || _currentDefinition == null || _currentTaskState == null)
            return;

        _processingTransition = true;
        int reward = Math.Max(0, _currentDefinition.completionRewardGems);
        if (!_currentTaskState.completionRewardGranted)
        {
            _currentTaskState.completionRewardGranted = true;
            _currentTaskState.rewardGranted = true;
            SaveState();
            if (reward > 0)
                G.Currency?.AddCurrency(CurrencyType.Gems, reward);
        }

        bool autoCompleted = _currentTaskState.objectiveAutoCompleted;
        _state.MarkTerminal(CurrentStableId, wasSkipped: false, UtcNowUnix());
        SaveState();
        Dictionary<string, object> parameters = BuildStepParameters();
        parameters["auto_completed"] = autoCompleted;
        LogEvent("tutorial_step_completed", parameters);

        _awaitingRewardClaim = false;
        SetTargets(null, null, null);
        _view?.ShowClaimed(reward > 0
            ? L("UI/Tutorial/RewardClaimed", "REWARD CLAIMED!")
            : L("UI/Tutorial/Completed", "TASK COMPLETE!"));
        StartCoroutine(AdvanceAfterCompletion());
    }

    private IEnumerator AdvanceAfterCompletion()
    {
        yield return new WaitForSecondsRealtime(CompletionPresentationSeconds);
        _processingTransition = false;
        _awaitingRewardClaim = false;
        _currentDefinition = null;
        _currentTaskState = null;
        TryStartNextAvailable();
    }

    private void SuspendCurrentStep()
    {
        if (_currentDefinition == null)
            return;

        _state.Suspend(CurrentStableId, UtcNowUnix());
        SaveState();
        _currentDefinition = null;
        _currentTaskState = null;
        _awaitingRewardClaim = false;
        _active = false;
        SetTargets(null, null, null);
        TryStartNextAvailable();
    }

    private void PauseSessionUntilContext()
    {
        if (_active)
            G.Ad?.SetTutorialInterstitialSuppressed(false, PostTutorialInterstitialGraceSeconds);

        _active = false;
        _currentDefinition = null;
        _currentTaskState = null;
        SetTargets(null, null, null);
        if (_view != null)
            _view.gameObject.SetActive(false);

        if (_state.AreAllKnownStepsTerminal())
        {
            _schedulerReady = false;
            G.Save.SaveTutorialProgress(true);
            LogEvent("tutorial_completed", new Dictionary<string, object>
            {
                ["task_count"] = TutorialStepCatalog.Steps.Length,
                ["reward_gems_total"] = 32,
            });
        }
    }

    private void OnTutorialSignal(TutorialSignal signal)
    {
        if (_state == null)
            return;

        CaptureTutorialEntity(signal);

        if (!_active || _processingTransition || _currentDefinition == null || !IsSignalFromLocalGameplay(signal))
            return;

        bool completes = false;
        switch (_currentDefinition.completionTrigger)
        {
            case TutorialCompletionTrigger.EggPurchased:
                completes = signal.Type == TutorialSignalType.ItemAcquired && signal.ItemType == Item.Egg;
                break;
            case TutorialCompletionTrigger.EggPlaced:
                completes = signal.Type == TutorialSignalType.EggPlaced;
                break;
            case TutorialCompletionTrigger.EggSpeedupUsed:
                completes = signal.Type == TutorialSignalType.EggSpeedupUsed;
                break;
            case TutorialCompletionTrigger.EggHatched:
                completes = signal.Type == TutorialSignalType.AnimalHatched;
                break;
            case TutorialCompletionTrigger.AlbumRewardsClaimed:
                completes = signal.Type == TutorialSignalType.AlbumRewardClaimed && AreTutorialAlbumRewardsClaimed();
                break;
            case TutorialCompletionTrigger.BigPetPurchased:
                completes = signal.Type == TutorialSignalType.BigPetPurchased;
                break;
            case TutorialCompletionTrigger.BigPetFed:
                completes = signal.Type == TutorialSignalType.BigPetFed;
                break;
            case TutorialCompletionTrigger.TerritoryUnlocked:
                completes = signal.Type == TutorialSignalType.FieldUnlocked;
                break;
            case TutorialCompletionTrigger.ConveyorUpgraded:
                completes = signal.Type == TutorialSignalType.ConveyorUpgraded;
                break;
        }

        if (completes)
            CompleteCurrentStep(autoCompleted: false);
    }

    public bool TryUseFreeEggSpeedup(Egg egg)
    {
        if (_state == null || egg == null || egg.Status != EggStatus.Maturing || _state.freeEggSpeedupUsed)
            return false;

        _state.freeEggSpeedupUsed = true;
        SaveState();
        return true;
    }

    public bool TryGetGuaranteedStarterAnimal(Egg egg, out Brainrot animal)
    {
        animal = null;
        if (_state == null || egg == null || _state.starterAnimalGranted || G.Storage == null)
            return false;
        if (_establishedPlayerAtSessionStart && HasHatchedOrLaterProgress())
            return false;
        if (!string.IsNullOrWhiteSpace(_state.tutorialEggId) &&
            !string.Equals(_state.tutorialEggId, egg.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        animal = G.Storage.GetPet(StarterAnimalId);
        if (animal == null)
            return false;

        _state.starterAnimalGranted = true;
        SaveState();
        return true;
    }

    // Compatibility for older callers and editor utilities. Tutorial V2 never changes the real timer or egg price.
    public int GetHatchDurationSeconds(Egg egg, int originalDurationSeconds) => Math.Max(1, originalDurationSeconds);
    public bool TryPrepareStarterEggPurchase(Egg egg) => false;

    private void CaptureTutorialEntity(TutorialSignal signal)
    {
        bool changed = false;
        if (signal.Type == TutorialSignalType.ItemAcquired && signal.ItemType == Item.Egg && signal.Context is Egg egg)
        {
            if (string.IsNullOrWhiteSpace(_state.tutorialEggId))
            {
                _state.tutorialEggId = egg.Name;
                _state.tutorialEggElement = (int)NormalizeElement(egg.Data.DinamicData.ElementType);
                changed = true;
            }
        }
        else if (signal.Type == TutorialSignalType.EggPlaced && signal.Context is FieldCell eggCell)
        {
            Egg placed = eggCell.CurrentEgg;
            if (placed != null && string.IsNullOrWhiteSpace(_state.tutorialEggId))
            {
                _state.tutorialEggId = placed.Name;
                _state.tutorialEggElement = (int)NormalizeElement(placed.Data.DinamicData.ElementType);
                changed = true;
            }
        }
        else if (signal.Type == TutorialSignalType.AnimalHatched)
        {
            Brainrot animal = (signal.Context as FieldCell)?.CurrentBrainrot;
            _state.tutorialAnimalId = !string.IsNullOrWhiteSpace(signal.ItemId)
                ? signal.ItemId
                : animal != null ? animal.Name : _state.tutorialAnimalId;
            if (animal != null)
                _state.tutorialAnimalElement = (int)NormalizeElement(animal.DinamicData.ElementType);
            changed = true;
        }

        if (changed)
            SaveState();
    }

    private void CaptureKnownTutorialEntities()
    {
        if (_state == null)
            return;

        if (string.IsNullOrWhiteSpace(_state.tutorialEggId))
        {
            Egg egg = FindOwnedItem(Item.Egg) as Egg ?? FindLocalEggCell(null)?.CurrentEgg;
            if (egg != null)
            {
                _state.tutorialEggId = egg.Name;
                _state.tutorialEggElement = (int)NormalizeElement(egg.Data.DinamicData.ElementType);
            }
        }

        if (string.IsNullOrWhiteSpace(_state.tutorialAnimalId))
        {
            Brainrot animal = FindLocalAnimalCell()?.CurrentBrainrot ?? FindOwnedItem(Item.Brainrot) as Brainrot;
            if (animal != null)
            {
                _state.tutorialAnimalId = animal.Name;
                _state.tutorialAnimalElement = (int)NormalizeElement(animal.DinamicData.ElementType);
            }
        }
    }

    private static ElementType NormalizeElement(ElementType element)
    {
        return element == ElementType.ElementType ? ElementType.NoElement : element;
    }

    private bool AreTutorialAlbumRewardsClaimed()
    {
        if (G.Album == null)
            return false;

        CaptureKnownTutorialEntities();
        bool hasKnownReward = false;
        bool allClaimed = true;
        if (!string.IsNullOrWhiteSpace(_state.tutorialEggId))
        {
            hasKnownReward = true;
            allClaimed &= G.Album.IsRewardClaimed(
                AlbumEntityType.Egg,
                _state.tutorialEggId,
                NormalizeElement((ElementType)_state.tutorialEggElement));
        }
        if (!string.IsNullOrWhiteSpace(_state.tutorialAnimalId))
        {
            hasKnownReward = true;
            allClaimed &= G.Album.IsRewardClaimed(
                AlbumEntityType.Animal,
                _state.tutorialAnimalId,
                NormalizeElement((ElementType)_state.tutorialAnimalElement));
        }

        return hasKnownReward && allClaimed;
    }

    private void RefreshContext()
    {
        if (!_active || _currentDefinition == null)
            return;

        string message;
        Transform primary = null;
        Transform secondary = null;
        Transform highlight = null;

        switch (CurrentStep)
        {
            case TutorialStepId.LearnMovement:
                message = GetDefinitionText();
                break;
            case TutorialStepId.BuyFirstEgg:
                ResolveBuyEggContext(out message, out primary);
                highlight = primary;
                break;
            case TutorialStepId.PlaceFirstEgg:
                ResolvePlaceEggContext(out message, out primary, out secondary, out highlight);
                break;
            case TutorialStepId.UseFreeEggSpeedup:
                ResolveSpeedupContext(out message, out primary, out highlight);
                break;
            case TutorialStepId.HatchReadyEgg:
                ResolveHatchContext(out message, out primary, out highlight);
                break;
            case TutorialStepId.ClaimFirstAlbumRewards:
                message = L("UI/Tutorial/Context/Album", "Open the album and claim the remaining egg and animal rewards.");
                primary = FindAlbumTarget();
                break;
            case TutorialStepId.BuyBigPet:
                ResolveBigPetPurchaseContext(out message, out primary, out secondary, out highlight);
                break;
            case TutorialStepId.FeedBigPet:
                ResolveFeedContext(out message, out primary, out secondary, out highlight);
                break;
            case TutorialStepId.UnlockTerritory:
                ResolveTerritoryContext(out message, out primary, out highlight);
                break;
            case TutorialStepId.UpgradeConveyor:
                ResolveConveyorUpgradeContext(out message, out primary, out highlight);
                break;
            default:
                message = GetDefinitionText();
                break;
        }

        SetTargets(primary, secondary, highlight);
        RefreshView(message);
    }

    private void ResolveBuyEggContext(out string message, out Transform target)
    {
        EnsureAffordableEgg();
        Conveyor conveyor = FindLocalConveyor();
        Egg egg = conveyor?.FindNearestAffordableEgg(PlayerPosition, G.Currency?.Coins ?? 0d);
        double price = egg != null ? egg.EffectivePrice : G.Storage?.GetEgg(StarterEggId)?.EffectivePrice ?? 0d;
        message = FormatLocalized("UI/Tutorial/Context/BuyEgg", "Buy the marked egg for {0} coins.", FormatCoins(price));
        target = egg != null ? egg.transform : conveyor != null ? conveyor.transform : null;
    }

    private void ResolvePlaceEggContext(out string message, out Transform primary, out Transform secondary, out Transform highlight)
    {
        primary = secondary = highlight = null;
        if (!IsPlayerAtLocalHome())
        {
            message = L("UI/Tutorial/Context/ReturnHomeEgg", "Return to your farm with the egg.");
            primary = GetLocalHomeTarget();
            secondary = FindTeleportButton(ScenePoint.HOME);
            return;
        }

        InventoryItem egg = FindOwnedItem(Item.Egg);
        if (egg == null)
        {
            message = L("UI/Tutorial/Context/ReacquireEgg", "The egg is gone. Buy another affordable egg from your conveyor.");
            Conveyor conveyor = FindLocalConveyor();
            EnsureAffordableEgg();
            primary = conveyor?.FindNearestAffordableEgg(PlayerPosition, G.Currency?.Coins ?? 0d)?.transform ?? conveyor?.transform;
            highlight = primary;
            return;
        }

        if (G.QuickAccess.CurrentActive != egg)
        {
            ResolveEquipItemContext(
                egg,
                Item.Egg,
                "UI/Tutorial/Context/OpenInventoryEgg",
                "Open the inventory to find your egg.",
                "UI/Tutorial/Context/ChooseEggTab",
                "Choose the Eggs tab.",
                "UI/Tutorial/Context/AddEggQuick",
                "Tap the egg to add it to quick access.",
                "UI/Tutorial/Context/EquipEgg",
                "Select the egg in quick access.",
                out message,
                out primary);
            return;
        }

        FieldCell cell = FindLocalFreeCell();
        if (cell == null)
        {
            message = L("UI/Tutorial/Context/NoFreeCell", "Unlock or free a cell to place the egg.");
            return;
        }

        highlight = cell.transform;
        if (cell.IsPlayerOnCell)
        {
            message = Application.isMobilePlatform
                ? L("UI/Tutorial/Context/PlaceEggActionTouch", "Press the action button to place the egg here.")
                : L("UI/Tutorial/Context/PlaceEggActionDesktop", "Hold E to place the egg here.");
            primary = cell.DropActionTarget;
        }
        else
        {
            message = L("UI/Tutorial/Context/ApproachFreeCell", "Go to the highlighted free cell.");
            primary = cell.transform;
        }
    }

    private void ResolveSpeedupContext(out string message, out Transform primary, out Transform highlight)
    {
        FieldCell cell = FindLocalEggCell(EggStatus.Maturing);
        highlight = cell != null ? cell.transform : null;
        if (cell != null && cell.IsPlayerOnCell)
        {
            message = L("UI/Tutorial/Context/FreeSpeedupAction", "Finish maturation now. The first time is free; later it requires an ad.");
            primary = cell.SpeedupActionTarget;
        }
        else
        {
            message = L("UI/Tutorial/Context/ApproachMaturingEgg", "Approach the maturing egg. Your first instant maturation is free.");
            primary = cell != null ? cell.transform : null;
        }
    }

    private void ResolveHatchContext(out string message, out Transform primary, out Transform highlight)
    {
        FieldCell cell = FindLocalEggCell(EggStatus.ReadyToHatch);
        highlight = cell != null ? cell.transform : null;
        if (cell != null && cell.IsPlayerOnCell)
        {
            message = Application.isMobilePlatform
                ? L("UI/Tutorial/Context/HatchActionTouch", "Press the action button to hatch the ready egg.")
                : L("UI/Tutorial/Context/HatchActionDesktop", "Hold E to hatch the ready egg.");
            primary = cell.HatchActionTarget;
        }
        else
        {
            message = L("UI/Tutorial/Context/ApproachReadyEgg", "Approach the ready egg to hatch it.");
            primary = cell != null ? cell.transform : null;
        }
    }

    private void ResolveBigPetPurchaseContext(out string message, out Transform primary, out Transform secondary, out Transform highlight)
    {
        BigPetPoint bigPet = FindLocalBigPet();
        primary = secondary = highlight = null;
        double price = bigPet?.UnlockPrice ?? 0d;
        if ((G.Currency?.Coins ?? 0d) < price)
        {
            message = FormatLocalized(
                "UI/Tutorial/Context/SaveBigPet",
                "Save {0} coins for the big animal ({1}/{0}). Collect coins from placed animals.",
                FormatCoins(price),
                FormatCoins(G.Currency?.Coins ?? 0d));
            primary = FindIncomeTeachingTarget();
            highlight = primary;
            return;
        }

        if (!IsPlayerAtLocalHome())
        {
            message = L("UI/Tutorial/Context/ReturnHomeBigPet", "You have enough coins. Return home to buy the big animal.");
            primary = GetLocalHomeTarget();
            secondary = FindTeleportButton(ScenePoint.HOME);
            return;
        }

        highlight = bigPet != null ? bigPet.transform : null;
        if (bigPet != null && bigPet.IsPlayerInArea)
        {
            message = FormatLocalized("UI/Tutorial/Context/BuyBigPetAction", "Buy the big animal for {0} coins.", FormatCoins(price));
            primary = bigPet.BuyActionTarget;
        }
        else
        {
            message = L("UI/Tutorial/Context/ApproachBigPet", "Go to the big-animal place on your farm.");
            primary = bigPet != null ? bigPet.transform : null;
        }
    }

    private void ResolveFeedContext(out string message, out Transform primary, out Transform secondary, out Transform highlight)
    {
        primary = secondary = highlight = null;
        FoodShop shop = FindFoodShop();
        Food firstFood = shop?.GetFirstFood();
        shop?.EnsureTutorialFoodAvailable(firstFood);
        InventoryItem ownedFood = FindOwnedItem(Item.Food, firstFood != null ? firstFood.Name : null);

        if (ownedFood == null)
        {
            double price = firstFood != null ? firstFood.Data.MoneyPrice : 0d;
            if ((G.Currency?.Coins ?? 0d) < price)
            {
                message = FormatLocalized(
                    "UI/Tutorial/Context/SaveFood",
                    "Save {0} coins for the first fruit ({1}/{0}). Every big-animal level adds 10% farm income.",
                    FormatCoins(price),
                    FormatCoins(G.Currency?.Coins ?? 0d));
                primary = FindIncomeTeachingTarget();
                highlight = primary;
                return;
            }

            if (shop != null && !shop.IsPlayerInside)
            {
                message = L("UI/Tutorial/Context/TravelFoodShop", "Go to the food shop. You can use the FOOD teleport button.");
                primary = shop.TeleportPoint != null ? shop.TeleportPoint : shop.transform;
                secondary = FindTeleportButton(ScenePoint.FOOD);
                highlight = shop.transform;
                return;
            }

            message = FormatLocalized("UI/Tutorial/Context/BuyFoodAction", "Buy the first fruit for {0} coins.", FormatCoins(price));
            FoodShopSlot slot = shop?.Ui?.FindSlot(firstFood);
            primary = slot != null ? slot.CoinButtonTarget : shop?.transform;
            return;
        }

        if (G.QuickAccess.CurrentActive != ownedFood)
        {
            ResolveEquipItemContext(
                ownedFood,
                Item.Food,
                "UI/Tutorial/Context/OpenInventoryFood",
                "Open the inventory to find the fruit.",
                "UI/Tutorial/Context/ChooseFoodTab",
                "Choose the Food tab.",
                "UI/Tutorial/Context/AddFoodQuick",
                "Tap the fruit to add it to quick access.",
                "UI/Tutorial/Context/EquipFood",
                "Select the fruit in quick access.",
                out message,
                out primary);
            return;
        }

        if (!IsPlayerAtLocalHome())
        {
            message = L("UI/Tutorial/Context/ReturnHomeFood", "Return home with the fruit to feed the big animal.");
            primary = GetLocalHomeTarget();
            secondary = FindTeleportButton(ScenePoint.HOME);
            return;
        }

        BigPetPoint bigPet = FindLocalBigPet();
        highlight = bigPet != null ? bigPet.transform : null;
        if (bigPet != null && bigPet.IsPlayerInArea)
        {
            message = L("UI/Tutorial/Context/FeedAction", "Feed the fruit to the big animal. Each level adds 10% to all farm income.");
            primary = bigPet.FeedActionTarget;
        }
        else
        {
            message = L("UI/Tutorial/Context/ApproachBigPetWithFood", "Bring the fruit to the big animal.");
            primary = bigPet != null ? bigPet.transform : null;
        }
    }

    private void ResolveTerritoryContext(out string message, out Transform primary, out Transform highlight)
    {
        Field field = FindCheapestLockedField();
        primary = highlight = field != null ? field.transform : null;
        double price = field != null ? field.UnlockPrice : 0d;
        if ((G.Currency?.Coins ?? 0d) < price)
        {
            message = FormatLocalized(
                "UI/Tutorial/Context/SaveTerritory",
                "Save {0} coins for the cheapest territory ({1}/{0}).",
                FormatCoins(price),
                FormatCoins(G.Currency?.Coins ?? 0d));
            primary = FindIncomeTeachingTarget();
            highlight = primary;
            return;
        }

        InventoryItem hammer = FindOwnedItem(Item.Hamer);
        if (G.QuickAccess != null && G.QuickAccess.CheckHand() != Item.Hamer && hammer != null)
        {
            message = L("UI/Tutorial/Context/EquipHammer", "Select the hammer to unlock territory.");
            primary = FindQuickSlot(hammer)?.transform;
            highlight = null;
            return;
        }

        if (field != null && field.BuyActionTarget.gameObject.activeInHierarchy)
        {
            message = FormatLocalized("UI/Tutorial/Context/BuyTerritoryAction", "Unlock this territory for {0} coins.", FormatCoins(price));
            primary = field.BuyActionTarget;
        }
        else
        {
            message = L("UI/Tutorial/Context/ApproachTerritory", "Go to the highlighted cheapest territory.");
        }
    }

    private void ResolveConveyorUpgradeContext(out string message, out Transform primary, out Transform highlight)
    {
        Conveyor conveyor = FindLocalConveyor();
        primary = highlight = conveyor != null ? conveyor.transform : null;
        double price = conveyor?.NextUpgradePriceCoins ?? 0d;
        if ((G.Currency?.Coins ?? 0d) < price)
        {
            message = FormatLocalized(
                "UI/Tutorial/Context/SaveConveyor",
                "Save {0} coins for the next conveyor upgrade ({1}/{0}).",
                FormatCoins(price),
                FormatCoins(G.Currency?.Coins ?? 0d));
            primary = FindIncomeTeachingTarget();
            highlight = primary;
            return;
        }

        if (conveyor?.Ui != null && conveyor.Ui.IsOpen)
        {
            conveyor.Ui.ShowLevel(conveyor.NextUpgradeLevel);
            message = FormatLocalized("UI/Tutorial/Context/BuyConveyorAction", "Buy this conveyor upgrade for {0} coins.", FormatCoins(price));
            primary = conveyor.Ui.CoinBuyTarget;
            highlight = conveyor.transform;
        }
        else
        {
            message = L("UI/Tutorial/Context/ApproachConveyorUpgrade", "Go to your conveyor to open its upgrades.");
        }
    }

    private void ResolveEquipItemContext(
        InventoryItem item,
        Item tab,
        string openKey,
        string openFallback,
        string tabKey,
        string tabFallback,
        string addKey,
        string addFallback,
        string equipKey,
        string equipFallback,
        out string message,
        out Transform target)
    {
        target = null;
        InventoryUI inventoryUi = FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (!item.InQuickAccess)
        {
            if (inventoryUi == null || !inventoryUi.IsOpen)
            {
                message = L(openKey, openFallback);
                target = FindPersistentButton(inventoryUi, "_ToggleOpen");
                return;
            }
            if (inventoryUi.SelectedTab != tab)
            {
                message = L(tabKey, tabFallback);
                target = FindPersistentButton(inventoryUi, tab == Item.Egg ? "_ShowEggs" : "_ShowFood");
                return;
            }

            message = L(addKey, addFallback);
            target = inventoryUi.FindSlot(item)?.transform;
            return;
        }

        message = L(equipKey, equipFallback);
        target = FindQuickSlot(item)?.transform;
    }

    private void EnsureAffordableEgg()
    {
        if (Time.unscaledTime < _nextAffordableEggAttempt)
            return;
        _nextAffordableEggAttempt = Time.unscaledTime + 0.75f;

        Conveyor conveyor = FindLocalConveyor();
        if (conveyor == null || G.Currency == null)
            return;
        if (conveyor.FindNearestAffordableEgg(PlayerPosition, G.Currency.Coins) != null)
            return;

        Egg prefab = G.Storage?.GetEgg(StarterEggId);
        if (prefab != null && prefab.GetPriceForElement(ElementType.NoElement) <= G.Currency.Coins)
            conveyor.SpawnTutorialEgg(prefab);
    }

    private void EnsureTutorialFoodAvailable()
    {
        FoodShop shop = FindFoodShop();
        if (shop != null)
            shop.EnsureTutorialFoodAvailable(shop.GetFirstFood());
    }

    private InventoryItem FindOwnedItem(Item type, string requiredId = null)
    {
        if (G.Inventory != null)
        {
            var items = G.Inventory.GetItems(type);
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    InventoryItem item = items[i];
                    if (MatchesOwnedItem(item, type, requiredId))
                        return item;
                }
            }
        }

        if (G.QuickAccess?.Items != null)
        {
            for (int i = 0; i < G.QuickAccess.Items.Count; i++)
            {
                InventoryItem item = G.QuickAccess.Items[i];
                if (MatchesOwnedItem(item, type, requiredId))
                    return item;
            }
        }

        return null;
    }

    private static bool MatchesOwnedItem(InventoryItem item, Item type, string requiredId)
    {
        return item != null && item.Type == type &&
               (string.IsNullOrWhiteSpace(requiredId) ||
                string.Equals(item.Name, requiredId, StringComparison.OrdinalIgnoreCase));
    }

    private Transform FindIncomeTeachingTarget()
    {
        FieldCell cell = FindLocalAnimalCell(requireCollectibleIncome: true) ?? FindLocalAnimalCell();
        return cell != null ? cell.transform : null;
    }

    private QuickSlot FindQuickSlot(InventoryItem item)
    {
        QuickAccessPanelUI[] panels = FindObjectsByType<QuickAccessPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < panels.Length; i++)
        {
            QuickSlot slot = panels[i]?.FindSlot(item);
            if (slot != null && slot.gameObject.activeInHierarchy)
                return slot;
        }
        return null;
    }

    private Transform FindPersistentButton(UnityEngine.Object target, string methodName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Button fallback = null;
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;
            for (int j = 0; j < button.onClick.GetPersistentEventCount(); j++)
            {
                if (!string.Equals(button.onClick.GetPersistentMethodName(j), methodName, StringComparison.Ordinal))
                    continue;
                if (target != null && button.onClick.GetPersistentTarget(j) != target)
                    continue;
                if (button.gameObject.activeInHierarchy)
                    return button.transform;
                fallback = button;
            }
        }
        return fallback != null ? fallback.transform : null;
    }

    private Transform FindTeleportButton(ScenePoint point)
    {
        TeleportButton[] buttons = FindObjectsByType<TeleportButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].Destination == point && buttons[i].gameObject.activeInHierarchy)
                return buttons[i].transform;
        }
        return null;
    }

    private Transform FindAlbumTarget()
    {
        AlbumScreenController screen = FindAnyObjectByType<AlbumScreenController>(FindObjectsInactive.Include);
        if (screen != null && screen.IsOpen && TryGetNextAlbumReward(out AlbumEntityType type, out string id, out ElementType element))
        {
            if (screen.CurrentTab != type)
                return screen.GetTopTabTarget(type);
            if (!screen.SelectedElement.HasValue || screen.SelectedElement.Value != element)
                return screen.FindElementTabTarget(element);

            Transform cardTarget = screen.FindCardTarget(type, id);
            AlbumEntryView card = cardTarget != null ? cardTarget.GetComponent<AlbumEntryView>() : null;
            return card != null && card.IsSelected ? screen.RewardActionTarget : cardTarget;
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || !buttons[i].gameObject.activeInHierarchy)
                continue;
            if (BuildHierarchyName(buttons[i].transform).IndexOf("album", StringComparison.OrdinalIgnoreCase) >= 0)
                return buttons[i].transform;
        }
        return screen != null ? screen.transform : null;
    }

    private bool TryGetNextAlbumReward(out AlbumEntityType type, out string id, out ElementType element)
    {
        type = AlbumEntityType.Egg;
        id = string.Empty;
        element = ElementType.NoElement;
        if (G.Album == null || _state == null)
            return false;

        if (!string.IsNullOrWhiteSpace(_state.tutorialEggId))
        {
            ElementType eggElement = NormalizeElement((ElementType)_state.tutorialEggElement);
            if (!G.Album.IsRewardClaimed(AlbumEntityType.Egg, _state.tutorialEggId, eggElement))
            {
                type = AlbumEntityType.Egg;
                id = _state.tutorialEggId;
                element = eggElement;
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(_state.tutorialAnimalId))
        {
            ElementType animalElement = NormalizeElement((ElementType)_state.tutorialAnimalElement);
            if (!G.Album.IsRewardClaimed(AlbumEntityType.Animal, _state.tutorialAnimalId, animalElement))
            {
                type = AlbumEntityType.Animal;
                id = _state.tutorialAnimalId;
                element = animalElement;
                return true;
            }
        }

        return false;
    }

    private Conveyor FindLocalConveyor()
    {
        Transform root = ResolveLocalRoot();
        Conveyor[] all = root != null
            ? root.GetComponentsInChildren<Conveyor>(true)
            : FindObjectsByType<Conveyor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].IsRemoteMode)
                return all[i];
        }
        return null;
    }

    private BigPetPoint FindLocalBigPet()
    {
        Transform root = ResolveLocalRoot();
        BigPetPoint[] all = root != null
            ? root.GetComponentsInChildren<BigPetPoint>(true)
            : FindObjectsByType<BigPetPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].IsRemoteMode)
                return all[i];
        }
        return null;
    }

    private FoodShop FindFoodShop()
    {
        return FindAnyObjectByType<FoodShop>(FindObjectsInactive.Include);
    }

    private FieldCell FindLocalFreeCell()
    {
        FieldCell[] cells = GetLocalCells();
        FieldCell best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            FieldCell cell = cells[i];
            if (cell == null || cell.IsRemoteMode || !cell.IsFree || !cell.gameObject.activeInHierarchy)
                continue;
            float distance = (cell.transform.position - PlayerPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = cell;
                bestDistance = distance;
            }
        }
        return best;
    }

    private FieldCell FindLocalEggCell(EggStatus? requiredStatus)
    {
        FieldCell[] cells = GetLocalCells();
        FieldCell best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            FieldCell cell = cells[i];
            Egg egg = cell != null && !cell.IsRemoteMode ? cell.CurrentEgg : null;
            if (egg == null || (requiredStatus.HasValue && egg.Status != requiredStatus.Value))
                continue;
            float distance = (cell.transform.position - PlayerPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = cell;
                bestDistance = distance;
            }
        }
        return best;
    }

    private FieldCell FindLocalAnimalCell(bool requireCollectibleIncome = false)
    {
        FieldCell[] cells = GetLocalCells();
        FieldCell best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < cells.Length; i++)
        {
            FieldCell cell = cells[i];
            Brainrot animal = cell != null && !cell.IsRemoteMode ? cell.CurrentBrainrot : null;
            if (animal == null || (requireCollectibleIncome && !animal.HasCollectibleIncome))
                continue;
            float distance = (cell.transform.position - PlayerPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = cell;
                bestDistance = distance;
            }
        }
        return best;
    }

    private Field FindCheapestLockedField()
    {
        Transform root = ResolveLocalRoot();
        Field[] fields = root != null
            ? root.GetComponentsInChildren<Field>(true)
            : FindObjectsByType<Field>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Field best = null;
        float bestPrice = float.MaxValue;
        for (int i = 0; i < fields.Length; i++)
        {
            Field field = fields[i];
            if (field == null || field.IsRemoteMode || field.IsUnblocked || field.UnlockPrice >= bestPrice)
                continue;
            best = field;
            bestPrice = field.UnlockPrice;
        }
        return best;
    }

    private bool HasUnlockedExpansion()
    {
        Transform root = ResolveLocalRoot();
        Field[] fields = root != null
            ? root.GetComponentsInChildren<Field>(true)
            : FindObjectsByType<Field>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fields.Length; i++)
        {
            Field field = fields[i];
            if (field != null && !field.IsRemoteMode && !field.DefaultUnblocked && field.IsUnblocked)
                return true;
        }
        return false;
    }

    private bool HasOwnedEggOrLaterProgress()
    {
        return FindOwnedItem(Item.Egg) != null || HasPlacedEggOrLaterProgress();
    }

    private bool HasPlacedEggOrLaterProgress()
    {
        return FindLocalEggCell(null) != null || HasHatchedOrLaterProgress();
    }

    private bool HasHatchedOrLaterProgress()
    {
        return FindLocalAnimalCell() != null || FindOwnedItem(Item.Brainrot) != null ||
               FindLocalBigPet()?.IsPurchased == true || HasUnlockedExpansion() || FindLocalConveyor()?.HasAnyUpgrade == true;
    }

    private bool HasAnyEstablishedProgress()
    {
        return HasOwnedEggOrLaterProgress() || FindLocalBigPet()?.IsPurchased == true ||
               HasUnlockedExpansion() || FindLocalConveyor()?.HasAnyUpgrade == true;
    }

    private bool HasFedBigPet()
    {
        return G.Save != null && (G.Save.LoadBigPetXP() > 0 || G.Save.LoadBigPetLvl() > 1);
    }

    private bool IsSignalFromLocalGameplay(TutorialSignal signal)
    {
        if (signal.Context == null)
            return true;
        if (signal.Context is FieldCell cell)
            return !cell.IsRemoteMode && IsWithinLocalRoot(cell.transform);
        if (signal.Context is Field field)
            return !field.IsRemoteMode && IsWithinLocalRoot(field.transform);
        if (signal.Context is Conveyor conveyor)
            return !conveyor.IsRemoteMode && IsWithinLocalRoot(conveyor.transform);
        if (signal.Context is BigPetPoint bigPet)
            return !bigPet.IsRemoteMode && IsWithinLocalRoot(bigPet.transform);
        return true;
    }

    private FieldCell[] GetLocalCells()
    {
        Transform root = ResolveLocalRoot();
        if (root != null)
            return root.GetComponentsInChildren<FieldCell>(true);

        FieldCell[] all = FindObjectsByType<FieldCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var local = new List<FieldCell>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].IsRemoteMode)
                local.Add(all[i]);
        }
        return local.ToArray();
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

    private Transform GetLocalHomeTarget()
    {
        return GetRemoteBases()?.GetLocalSlotEntryPoint() ?? ResolveLocalRoot() ?? FindLocalConveyor()?.transform;
    }

    private bool IsWithinLocalRoot(Transform context)
    {
        Transform root = ResolveLocalRoot();
        return context != null && (root == null || context == root || context.IsChildOf(root));
    }

    private bool IsPlayerAtLocalHome()
    {
        Transform home = GetLocalHomeTarget();
        if (home == null || G.Player == null)
            return true;
        Vector3 delta = G.Player.transform.position - home.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= LocalHomeRadius * LocalHomeRadius;
    }

    private Vector3 PlayerPosition => G.Player != null ? G.Player.transform.position : Vector3.zero;

    private void SetTargets(Transform primary, Transform secondary, Transform highlight)
    {
        _primaryTarget = primary;
        _secondaryTarget = secondary;
        _view?.SetWorldTargets(_primaryTarget, _secondaryTarget);
        _highlighter?.SetTarget(highlight);
    }

    private void CreateView()
    {
        if (_view != null)
            return;

        TutorialView prefab = Resources.Load<TutorialView>(ViewResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[Tutorial] Missing Resources/{ViewResourcePath}.prefab");
            return;
        }

        Canvas canvas = FindBestScreenCanvas();
        _view = canvas != null ? Instantiate(prefab, canvas.transform) : Instantiate(prefab);
        _view.name = "TutorialView";
        _view.Initialize();
        _view.RewardClaimPressed -= OnRewardClaimPressed;
        _view.RewardClaimPressed += OnRewardClaimPressed;
        _view.gameObject.SetActive(false);
    }

    private static Canvas FindBestScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas best = null;
        int bestOrder = int.MinValue;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy ||
                canvas.renderMode == RenderMode.WorldSpace)
                continue;

            if (canvas.name.StartsWith("GameCanvas", StringComparison.OrdinalIgnoreCase))
                return canvas;

            int order = canvas.sortingOrder;
            if (best == null || order > bestOrder)
            {
                best = canvas;
                bestOrder = order;
            }
        }
        return best;
    }

    private void RefreshView(string message)
    {
        if (_view == null || _currentDefinition == null)
            return;

        int index = TutorialStepCatalog.FindIndex(CurrentStableId, 0) + 1;
        string progress = FormatLocalized("UI/Tutorial/Progress", "Tutorial {0}/{1}", index, TutorialStepCatalog.Steps.Length);
        int reward = Math.Max(0, _currentDefinition.completionRewardGems);
        string rewardText = reward > 0
            ? FormatLocalized("UI/Tutorial/Reward", "Reward: +{0}", reward)
            : L("UI/Tutorial/NoReward", "Training task");
        Sprite rewardIcon = reward > 0 ? G.Currency?.GetCurrencyIcon(CurrencyType.Gems) : null;
        _view.SetStep(progress, message, rewardText, rewardIcon, string.Empty, isFinalStep: false);
    }

    private string GetDefinitionText()
    {
        if (_currentDefinition == null)
            return string.Empty;
        bool touch = Application.isMobilePlatform;
        return L(
            touch ? _currentDefinition.touchTextKey : _currentDefinition.desktopTextKey,
            touch ? _currentDefinition.touchFallback : _currentDefinition.desktopFallback);
    }

    private string FormatCoins(double value)
    {
        return G.Currency != null
            ? G.Currency.ToString(value)
            : Math.Max(0d, value).ToString("0", CultureInfo.InvariantCulture);
    }

    private void SaveState()
    {
        if (_state == null || G.Save == null || !G.Save.IsReady)
            return;
        G.Save.SaveTutorialState(_state);
    }

    private Dictionary<string, object> BuildStepParameters()
    {
        long now = UtcNowUnix();
        long stepStart = _currentTaskState != null && _currentTaskState.startedUnix > 0
            ? _currentTaskState.startedUnix
            : now;
        int stepIndex = _currentDefinition != null ? TutorialStepCatalog.FindIndex(CurrentStableId, 0) : -1;
        return new Dictionary<string, object>
        {
            ["step_id"] = CurrentStableId,
            ["step_index"] = stepIndex,
            ["task_id"] = CurrentStableId,
            ["task_index"] = stepIndex,
            ["pack_id"] = _currentDefinition?.packId ?? string.Empty,
            ["definition_revision"] = _currentDefinition?.definitionRevision ?? 0,
            ["elapsed_sec"] = Math.Max(0L, now - stepStart),
            ["progress_value"] = _currentTaskState?.progressValue ?? 0d,
            ["activation_trigger"] = _currentDefinition?.activationTrigger.ToString() ?? string.Empty,
            ["completion_trigger"] = _currentDefinition?.completionTrigger.ToString() ?? string.Empty,
            ["completion_reward_gems"] = _currentDefinition?.completionRewardGems ?? 0,
            ["completion_reward_granted"] = _currentTaskState?.completionRewardGranted ?? false,
            ["input_mode"] = G.Control != null && G.Control.UseTouchControl ? "touch" : "desktop",
            ["online_mode"] = LobbyClient.Instance != null && LobbyClient.Instance.IsOnline ? "online" : "offline",
        };
    }

    private static void LogEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (AnalyticsManager.Instance != null)
            AnalyticsManager.Instance.LogEvent(eventName, parameters);
    }

    private void SubscribeLocalization()
    {
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLocalization()
    {
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLocalizationManagerReady(LocalizationManager manager)
    {
        if (manager == null)
            return;
        manager.OnLanguageChanged -= OnLanguageChanged;
        manager.OnLanguageChanged += OnLanguageChanged;
        RefreshLocalizedTutorialView();
    }

    private void OnLanguageChanged(string _)
    {
        RefreshLocalizedTutorialView();
    }

    private void RefreshLocalizedTutorialView()
    {
        if (_awaitingRewardClaim)
            PresentClaimableCompletion();
        else
            RefreshContext();
    }

    private static string L(string key, string fallback)
    {
        return LocalizationUtils.T(key, fallback);
    }

    private static string FormatLocalized(string key, string fallback, params object[] args)
    {
        string format = L(key, fallback);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.InvariantCulture, fallback, args);
        }
    }

    private static string BuildHierarchyName(Transform current)
    {
        string result = string.Empty;
        int depth = 0;
        while (current != null && depth++ < 7)
        {
            result += "/" + current.name;
            current = current.parent;
        }
        return result;
    }

    private static long UtcNowUnix()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

#if UNITY_EDITOR
    public void EditorDebugSetStep(int stepIndex)
    {
        if (_state == null || TutorialStepCatalog.Steps.Length == 0)
            return;
        int clamped = Mathf.Clamp(stepIndex, 0, TutorialStepCatalog.Steps.Length - 1);
        for (int i = 0; i < TutorialStepCatalog.Steps.Length; i++)
        {
            TutorialTaskSaveData task = _state.GetOrCreateTaskState(TutorialStepCatalog.Steps[i]);
            task.status = i < clamped ? TutorialTaskStatus.Completed : TutorialTaskStatus.Unseen;
            task.completionRewardGranted = i < clamped;
            task.rewardGranted = i < clamped;
            task.objectiveCompleted = false;
            task.objectiveAutoCompleted = false;
        }
        _editorDebugFreezeProgress = true;
        BeginDefinition(TutorialStepCatalog.Steps[clamped], resumed: false);
    }

    public void EditorDebugResumeProgress()
    {
        _editorDebugFreezeProgress = false;
    }
#endif
}

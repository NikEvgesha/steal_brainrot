using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private TutorialView _view;
    private TutorialTargetHighlighter _highlighter;
    private TutorialUiBlocker _uiBlocker;
    private RemoteBasesApplier _remoteBases;
    private Transform _currentTarget;
    private Vector3 _lastMovementPosition;
    private float _movementDistance;
    private float _stepActivatedRealtime;
    private float _nextTargetRefresh;
    private bool _active;
    private bool _inlineSkipConfirmation;
    private bool _starterGrantAttempted;
    private Coroutine _startRoutine;

    public bool IsTutorialActive => _active;
    public bool AllowsAlbum => _active && _state != null && CurrentStep == TutorialStepId.ClaimAlbumReward;
    public TutorialStepId CurrentStep => TutorialStepCatalog.Steps[CurrentStepIndex].id;
    private int CurrentStepIndex => _state != null
        ? Mathf.Clamp(_state.stepIndex, 0, TutorialStepCatalog.Steps.Length - 1)
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
        while (G.Save == null || !G.Save.IsReady || G.Player == null ||
               G.Inventory == null || !G.Inventory.IsInitialized || G.Storage == null)
        {
            yield return null;
        }

        _startRoutine = null;
        _state = G.Save.LoadTutorialState();
        if (_state == null)
            _state = TutorialSaveData.CreateNew();
        _state.Normalize();

        if (_state.completed || G.Save.GetTutorialProgress())
        {
            _state.completed = true;
            G.Save.SaveTutorialState(_state);
            yield break;
        }

        CreateView();
        CloseConflictingWindows();

        bool resumed = _state.startedUnix > 0;
        long now = UtcNowUnix();
        if (_state.startedUnix <= 0)
            _state.startedUnix = now;
        if (_state.stepStartedUnix <= 0)
            _state.stepStartedUnix = now;

        _active = true;
        G.Ad?.SetTutorialInterstitialSuppressed(true);
        SaveState();

        LogEvent(resumed ? "tutorial_resumed" : "tutorial_started", BuildStepParameters());
        ActivateCurrentStep(logStepStarted: true);
    }

    private void Update()
    {
        if (!_active || _state == null)
            return;

        RefreshTargetIfNeeded();
        EvaluateCurrentStep();
    }

    private void EvaluateCurrentStep()
    {
        float activeFor = Time.realtimeSinceStartup - _stepActivatedRealtime;
        switch (CurrentStep)
        {
            case TutorialStepId.LearnMovement:
                EvaluateMovement();
                break;

            case TutorialStepId.FindHome:
            case TutorialStepId.ReachConveyor:
            case TutorialStepId.ReturnHome:
                if (activeFor >= MinimumTargetStepDisplaySeconds && IsPlayerNear(_currentTarget, TargetReachDistance))
                    CompleteCurrentStep();
                break;

            case TutorialStepId.AcquireStarterEgg:
                if (HasStarterProgressItem())
                {
                    if (!_state.starterEggGranted)
                    {
                        _state.starterEggGranted = true;
                        SaveState();
                    }
                    CompleteCurrentStep();
                }
                else if (!_starterGrantAttempted && activeFor >= 0.4f)
                {
                    _starterGrantAttempted = true;
                    GrantStarterEggOnce();
                }
                break;

            case TutorialStepId.PlaceStarterEgg:
                if (FindLocalEggCell() != null || FindLocalAnimalCell() != null)
                    CompleteCurrentStep();
                break;

            case TutorialStepId.HatchStarterEgg:
                if (FindLocalAnimalCell() != null)
                {
                    _state.starterAnimalGranted = true;
                    SaveState();
                    CompleteCurrentStep();
                }
                break;

            case TutorialStepId.MeetStarterAnimal:
                if (activeFor >= 1.4f && FindLocalAnimalCell() != null)
                    CompleteCurrentStep();
                break;

            case TutorialStepId.WaitForFirstIncome:
                FieldCell incomeCell = FindLocalAnimalCell();
                if (incomeCell != null && incomeCell.CurrentBrainrot != null && incomeCell.CurrentBrainrot.CurrentIncome > 0d)
                    CompleteCurrentStep();
                break;

            case TutorialStepId.ClaimAlbumReward:
                if (HasClaimedStarterAlbumReward())
                    CompleteCurrentStep();
                break;
        }
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

        if (_movementDistance >= MovementDistanceRequired)
            CompleteCurrentStep();
    }

    private void ActivateCurrentStep(bool logStepStarted)
    {
        _state.Normalize();
        _stepActivatedRealtime = Time.realtimeSinceStartup;
        _movementDistance = 0f;
        _lastMovementPosition = G.Player != null ? G.Player.transform.position : Vector3.zero;
        _starterGrantAttempted = false;
        _inlineSkipConfirmation = false;

        RefreshView();
        RefreshTarget(force: true);
        _uiBlocker?.SetAlbumAllowed(CurrentStep == TutorialStepId.ClaimAlbumReward);

        if (logStepStarted)
            LogEvent("tutorial_step_started", BuildStepParameters());
    }

    private void CompleteCurrentStep()
    {
        if (!_active || _state == null)
            return;

        LogEvent("tutorial_step_completed", BuildStepParameters());
        int nextIndex = CurrentStepIndex + 1;
        if (nextIndex >= TutorialStepCatalog.Steps.Length)
        {
            CompleteTutorial(skipped: false);
            return;
        }

        _state.stepIndex = nextIndex;
        _state.stepId = TutorialStepCatalog.Steps[nextIndex].stableId;
        _state.stepStartedUnix = UtcNowUnix();
        SaveState();
        ActivateCurrentStep(logStepStarted: true);
    }

    private void CompleteTutorial(bool skipped)
    {
        if (!_active || _state == null)
            return;

        _state.completed = true;
        _state.skipped = skipped;
        _state.completedUnix = UtcNowUnix();
        SaveState();
        G.Save.SaveTutorialProgress(true);

        LogEvent(skipped ? "tutorial_skipped" : "tutorial_completed", BuildStepParameters());

        _active = false;
        G.Ad?.SetTutorialInterstitialSuppressed(false, PostTutorialInterstitialGraceSeconds);
        _uiBlocker?.End();
        _highlighter?.ClearTarget();
        if (_view != null)
            Destroy(_view.gameObject);
        _view = null;
    }

    public void QuickStopTutorial()
    {
        RequestSkip();
    }

    private void RequestSkip()
    {
        if (!_active)
            return;

        var request = new UniversalDecisionPopup.Request
        {
            title = new UniversalDecisionPopup.LocalizedTextPayload("UI/Tutorial/SkipTitle", "Skip tutorial?"),
            description = new UniversalDecisionPopup.LocalizedTextPayload(
                "UI/Tutorial/SkipDescription",
                "You can continue without hints. The starter reward will not be issued again."),
            confirm = new UniversalDecisionPopup.LocalizedTextPayload("UI/Tutorial/SkipConfirm", "Skip"),
            cancel = new UniversalDecisionPopup.LocalizedTextPayload("UI/Tutorial/SkipCancel", "Continue"),
            onConfirm = () => CompleteTutorial(skipped: true),
            onCancel = CancelInlineSkipConfirmation
        };

        if (FriendsPanelController.TryShowPopup(request))
            return;

        UniversalDecisionPopup popup = FindAnyObjectByType<UniversalDecisionPopup>(FindObjectsInactive.Include);
        if (popup != null)
        {
            popup.Show(request);
            return;
        }

        _inlineSkipConfirmation = true;
        _view?.ShowInlineSkipConfirmation(
            L("UI/Tutorial/SkipTitle", "Skip tutorial?"),
            L("UI/Tutorial/SkipDescription", "You can continue without hints. The starter reward will not be issued again."),
            L("UI/Tutorial/SkipConfirm", "Skip"));
    }

    private void CancelInlineSkipConfirmation()
    {
        if (!_inlineSkipConfirmation)
            return;

        _inlineSkipConfirmation = false;
        RefreshView();
    }

    private void OnPrimaryPressed()
    {
        if (CurrentStep == TutorialStepId.ContinueIndependently)
        {
            CompleteTutorial(skipped: false);
            return;
        }

        if (_inlineSkipConfirmation)
        {
            CompleteTutorial(skipped: true);
            return;
        }

        RequestSkip();
    }

    private void OnSecondaryPressed()
    {
        if (_inlineSkipConfirmation)
            CancelInlineSkipConfirmation();
        else
            RequestSkip();
    }

    private void OnTutorialSignal(TutorialSignal signal)
    {
        if (!_active || !IsSignalFromLocalGameplay(signal))
            return;

        switch (signal.Type)
        {
            case TutorialSignalType.ItemAcquired:
                if (signal.ItemType != Item.Egg)
                    return;

                if (CurrentStep == TutorialStepId.AcquireStarterEgg)
                {
                    _state.starterEggGranted = true;
                    SaveState();
                    CompleteCurrentStep();
                }
                else if (CurrentStep == TutorialStepId.MakeFirstExpansion)
                {
                    CompleteCurrentStep();
                }
                break;

            case TutorialSignalType.EggPlaced:
                if (CurrentStep == TutorialStepId.PlaceStarterEgg)
                    CompleteCurrentStep();
                break;

            case TutorialSignalType.AnimalHatched:
                _state.starterAnimalGranted = true;
                SaveState();
                if (CurrentStep == TutorialStepId.HatchStarterEgg)
                    CompleteCurrentStep();
                break;

            case TutorialSignalType.IncomeReady:
                if (CurrentStep == TutorialStepId.WaitForFirstIncome)
                    CompleteCurrentStep();
                break;

            case TutorialSignalType.IncomeCollected:
                if (CurrentStep == TutorialStepId.CollectFirstIncome && signal.Value > 0d)
                    CompleteCurrentStep();
                break;

            case TutorialSignalType.FieldUnlocked:
            case TutorialSignalType.ConveyorUpgraded:
                if (CurrentStep == TutorialStepId.MakeFirstExpansion)
                    CompleteCurrentStep();
                break;

            case TutorialSignalType.AlbumRewardClaimed:
                if (CurrentStep == TutorialStepId.ClaimAlbumReward)
                    CompleteCurrentStep();
                break;
        }
    }

    public int GetHatchDurationSeconds(Egg egg, int originalDurationSeconds)
    {
        if (!_active || _state == null || egg == null || _state.starterAnimalGranted)
            return originalDurationSeconds;
        if (CurrentStepIndex > (int)TutorialStepId.HatchStarterEgg)
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
        if (CurrentStepIndex > (int)TutorialStepId.HatchStarterEgg)
            return false;
        if (!string.Equals(egg.Name, StarterEggId, StringComparison.OrdinalIgnoreCase))
            return false;

        animal = G.Storage != null ? G.Storage.GetPet(StarterAnimalId) : null;
        return animal != null && Egg.IsAnimalDrop(animal);
    }

    private void GrantStarterEggOnce()
    {
        if (_state.starterEggGranted || HasStarterProgressItem())
            return;

        Egg prefab = G.Storage.GetEgg(StarterEggId);
        if (prefab == null)
        {
            Debug.LogError($"[Tutorial] Starter egg '{StarterEggId}' was not found in ItemPrefabStorage.");
            return;
        }

        Egg egg = Instantiate(prefab);
        var data = egg.Data.DinamicData;
        data.ElementType = ElementType.NoElement;
        data.WeightMultiplier = 1f;
        data.ResultIncome = 0d;
        egg.SetData(data);
        G.Inventory.Add(egg);

        _state.starterEggGranted = true;
        SaveState();
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
        switch (CurrentStep)
        {
            case TutorialStepId.FindHome:
            case TutorialStepId.ReturnHome:
                return GetRemoteBases()?.GetLocalSlotEntryPoint() ?? ResolveLocalRoot();

            case TutorialStepId.ReachConveyor:
            case TutorialStepId.AcquireStarterEgg:
            case TutorialStepId.MakeFirstExpansion:
                return FindLocalConveyor()?.transform;

            case TutorialStepId.PlaceStarterEgg:
                return FindLocalFreeCell()?.transform;

            case TutorialStepId.HatchStarterEgg:
                return FindLocalEggCell()?.transform;

            case TutorialStepId.MeetStarterAnimal:
            case TutorialStepId.WaitForFirstIncome:
            case TutorialStepId.CollectFirstIncome:
                return FindLocalAnimalCell()?.transform;

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
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null && !cells[i].IsRemoteMode && cells[i].CurrentEgg != null)
                return cells[i];
        }

        return null;
    }

    private FieldCell FindLocalAnimalCell()
    {
        FieldCell[] cells = GetLocalCells();
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null && !cells[i].IsRemoteMode && cells[i].CurrentBrainrot != null)
                return cells[i];
        }

        return null;
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
        _view.PrimaryPressed += OnPrimaryPressed;
        _view.SecondaryPressed += OnSecondaryPressed;
        _uiBlocker?.Begin(_view);
    }

    private static Canvas FindBestScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas best = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                continue;
            if (canvas.name == "GameCanvas")
                return canvas;
            if (best == null || canvas.sortingOrder > best.sortingOrder)
                best = canvas;
        }

        return best;
    }

    private void RefreshView()
    {
        if (_view == null || _state == null)
            return;

        TutorialStepDefinition step = TutorialStepCatalog.Steps[CurrentStepIndex];
        bool touch = G.Control != null && G.Control.UseTouchControl;
        string message = touch
            ? L(step.touchTextKey, step.touchFallback)
            : L(step.desktopTextKey, step.desktopFallback);
        string progress = LocalizationUtils.Format(
            "UI/Tutorial/Progress",
            "Tutorial {0}/{1}",
            CurrentStepIndex + 1,
            TutorialStepCatalog.Steps.Length);
        bool final = CurrentStep == TutorialStepId.ContinueIndependently;
        string button = final
            ? L("UI/Tutorial/Done", "Done")
            : L("UI/Tutorial/Skip", "Skip");

        _view.SetStep(progress, message, button, final);
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
        _state.Normalize();
        G.Save.SaveTutorialState(_state);
    }

    private Dictionary<string, object> BuildStepParameters()
    {
        TutorialStepDefinition step = TutorialStepCatalog.Steps[CurrentStepIndex];
        long now = UtcNowUnix();
        long stepStart = _state != null && _state.stepStartedUnix > 0 ? _state.stepStartedUnix : now;
        return new Dictionary<string, object>
        {
            ["step_id"] = step.stableId,
            ["step_index"] = CurrentStepIndex,
            ["elapsed_sec"] = Math.Max(0L, now - stepStart),
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
        if (_inlineSkipConfirmation)
        {
            _view?.ShowInlineSkipConfirmation(
                L("UI/Tutorial/SkipTitle", "Skip tutorial?"),
                L("UI/Tutorial/SkipDescription", "You can continue without hints. The starter reward will not be issued again."),
                L("UI/Tutorial/SkipConfirm", "Skip"));
            return;
        }

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

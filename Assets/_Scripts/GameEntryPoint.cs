using System;
using UnityEngine;

public class GameEntryPoint : MonoBehaviour
{
    [SerializeField] private Inventory _inventory;
    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private QuickAccessManager _quickAccess;
    [SerializeField] private GameObject _ui;
    [SerializeField] private GameObject _scene;
    [SerializeField] private ElementTypeMultiplaer _elements;
    [SerializeField] private ZooBackendClient _backend;

    [SerializeField] private IncomeModifiersHub _hubPrefab;
    [SerializeField] private DailyPlaytimeTrackerMB _dailyPlaytimeTracker;

    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private RemoteBasesApplier _remoteBasesApplier;
    [SerializeField] private float _initialLocationsWaitTimeout = 20f;

    private ZooBackendClient _runtimeBackend;
    [SerializeField] private bool _loadingHidden;
    private bool _offlineBasePrepared;
    private float _initialWorldWaitStartedAt;
    private float _nextInitialWorldReadyCheckAt;
    private float _nextInitialWorldExceptionLogAt;
    private bool _initialWorldWaitWarningLogged;
    [SerializeField] private string _initialWorldWaitReason = "not_started";

    private void Start()
    {
        _runtimeBackend = G.Backend;
        _initialWorldWaitStartedAt = Time.realtimeSinceStartup;

        Instantiate(_playerManager).Init(_playerSpawnPoint);
        FallRecoveryVolume.EnsureExists(_playerSpawnPoint);
        Instantiate(_elements);
        Instantiate(_quickAccess).Init();
        Instantiate(_inventory).Init();
        
        Instantiate(_dailyPlaytimeTracker);
        Instantiate(_hubPrefab).Initialize(G.Currency);
        PlayerLuckHub.EnsureExists();
        Instantiate(_ui);
        //Instantiate(_remoteBasesApplier);

        if (G.Backend == null && _backend != null)
            Instantiate(_backend);

        G.Initialized?.Invoke();
        TutorialManager.EnsureExists();
        LeaderboardWorldInstaller.EnsureExists();
        //_scene.SetActive(true);
    }

    private void Update()
    {
        if (_loadingHidden || Time.realtimeSinceStartup < _nextInitialWorldReadyCheckAt)
            return;

        _nextInitialWorldReadyCheckAt = Time.realtimeSinceStartup + 0.1f;

        try
        {
            if (TryFinalizeInitialWorld(out _initialWorldWaitReason))
            {
                HideLoadingScreen();
                return;
            }
        }
        catch (Exception exception)
        {
            if (Time.realtimeSinceStartup >= _nextInitialWorldExceptionLogAt)
            {
                _nextInitialWorldExceptionLogAt = Time.realtimeSinceStartup + 5f;
                Debug.LogException(new InvalidOperationException(
                    "GameEntryPoint: initial world readiness check failed. Retrying without dismissing the loading screen.",
                    exception));
            }

            return;
        }

        var warningAfter = Mathf.Max(5f, _initialLocationsWaitTimeout);
        if (!_initialWorldWaitWarningLogged &&
            Time.realtimeSinceStartup - _initialWorldWaitStartedAt >= warningAfter)
        {
            _initialWorldWaitWarningLogged = true;
            Debug.LogWarning(
                $"GameEntryPoint: initial world is still loading after {warningAfter:0.#}s. " +
                "Keeping the loading screen visible until the local base and player slot are ready.");
        }
    }

    private bool TryFinalizeInitialWorld(out string waitReason)
    {
        if (G.Save == null || !G.Save.IsReady)
        {
            waitReason = "save_not_ready";
            return false;
        }

        var lobby = LobbyClient.Instance;
        if (lobby != null && !lobby.IsInitialJoinResolved)
        {
            waitReason = "initial_lobby_join_pending";
            return false;
        }

        var remoteBases = ResolveRemoteBases();
        if (lobby != null && lobby.IsOnline)
        {
            if (remoteBases == null)
            {
                waitReason = "remote_bases_missing";
                return false;
            }
            if (G.Player == null)
            {
                waitReason = "player_missing";
                return false;
            }
            if (!remoteBases.TryGetResolvedLocalSlotRoot(out _))
            {
                int membersCount = lobby.LastMembers != null ? lobby.LastMembers.Count : 0;
                int matchingSlot = -1;
                string localPlayerId = G.Save.LoadBackendProfile().playerId;
                if (!string.IsNullOrEmpty(localPlayerId) && lobby.LastMembers != null)
                {
                    for (int i = 0; i < lobby.LastMembers.Count; i++)
                    {
                        var member = lobby.LastMembers[i];
                        if (member != null && member.playerId == localPlayerId)
                        {
                            matchingSlot = member.slotIndex;
                            break;
                        }
                    }
                }

                waitReason =
                    $"local_slot_not_resolved:members={membersCount},matching_slot={matchingSlot},mode={lobby.NetworkMode}";
                return false;
            }

            var entryPoint = remoteBases.GetLocalSlotEntryPoint();
            if (entryPoint == null)
            {
                waitReason = "local_entry_point_missing";
                return false;
            }

            var playerOffset = G.Player.transform.position - entryPoint.position;
            playerOffset.y = 0f;
            bool playerArrived = playerOffset.sqrMagnitude <= 4f;
            waitReason = playerArrived ? "ready_online" : "player_teleport_pending";
            return playerArrived;
        }

        if (lobby == null && _runtimeBackend != null && !_runtimeBackend.IsInitialLocationsLoaded)
        {
            waitReason = "backend_locations_pending";
            return false;
        }

        if (!_offlineBasePrepared)
        {
            PrepareOfflineLocalBase();
            _offlineBasePrepared = true;
            waitReason = "offline_base_prepared";
            return false;
        }

        bool offlineReady = remoteBases == null || remoteBases.TryGetResolvedLocalSlotRoot(out _);
        waitReason = offlineReady ? "ready_offline" : "offline_local_slot_not_resolved";
        return offlineReady;
    }

    private void PrepareOfflineLocalBase()
    {
        var remoteBases = ResolveRemoteBases();

        if (remoteBases != null)
            remoteBases.ApplyOfflineLocalOnly();
    }

    private RemoteBasesApplier ResolveRemoteBases()
    {
        bool hasLoadedSceneInstance =
            _remoteBasesApplier != null &&
            _remoteBasesApplier.gameObject.scene.IsValid() &&
            _remoteBasesApplier.gameObject.scene.isLoaded;

        if (!hasLoadedSceneInstance)
            _remoteBasesApplier = FindAnyObjectByType<RemoteBasesApplier>(FindObjectsInactive.Include);

        return _remoteBasesApplier;
    }

    private void HideLoadingScreen()
    {
        if (_loadingHidden)
            return;

        var loader = G.GameLoader != null
            ? G.GameLoader
            : FindAnyObjectByType<GameLoader>(FindObjectsInactive.Include);
        bool loadingScreenHidden;

        if (loader == null)
        {
            var progress = LoadingProgressBarUI.Instance != null
                ? LoadingProgressBarUI.Instance
                : FindAnyObjectByType<LoadingProgressBarUI>(FindObjectsInactive.Include);
            var loadingCanvas = progress != null ? progress.GetComponentInParent<Canvas>(true) : null;
            if (loadingCanvas == null)
            {
                _initialWorldWaitReason = "loading_screen_reference_missing";
                return;
            }

            loadingCanvas.gameObject.SetActive(false);
            loadingScreenHidden = true;
        }
        else
        {
            G.GameLoader = loader;
            loader.ShowLoadingScreen(false);
            loadingScreenHidden = true;
        }

        if (LoadingProgressBarUI.Instance != null &&
            LoadingProgressBarUI.Instance.gameObject.activeInHierarchy)
        {
            var loadingCanvas = LoadingProgressBarUI.Instance.GetComponentInParent<Canvas>(true);
            if (loadingCanvas != null)
                loadingCanvas.gameObject.SetActive(false);
        }

        if (!loadingScreenHidden)
            return;

        _loadingHidden = true;
        _initialWorldWaitReason = "loading_hidden";

        try
        {
            AnalyticsManager.Instance.MarkGameReady();
            AnalyticsManager.Instance.MarkGameplayStarted("game_entry");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"GameEntryPoint: gameplay is ready, but analytics notification failed: {exception.Message}");
        }
    }
}

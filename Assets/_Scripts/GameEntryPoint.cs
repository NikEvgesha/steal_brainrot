using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private float _initialLocationsWaitTimeout = 8f;

    private ZooBackendClient _runtimeBackend;
    private bool _loadingHidden;
    private Coroutine _initialLocationsTimeoutCoroutine;

    private void Start()
    {
        Instantiate(_playerManager).Init(_playerSpawnPoint);
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
        //_scene.SetActive(true);
        WaitForPlayerLocationsBeforeHideLoading();
    }

    private void OnDestroy()
    {
        if (_runtimeBackend != null)
            _runtimeBackend.InitialLocationsLoaded -= OnInitialLocationsLoaded;

        if (_initialLocationsTimeoutCoroutine != null)
            StopCoroutine(_initialLocationsTimeoutCoroutine);
    }

    private void WaitForPlayerLocationsBeforeHideLoading()
    {
        _runtimeBackend = G.Backend;
        if (_runtimeBackend == null)
        {
            PrepareOfflineLocalBase();
            HideLoadingScreen();
            return;
        }

        if (_runtimeBackend.IsInitialLocationsLoaded)
        {
            PrepareOfflineLocalBaseIfNeeded(_runtimeBackend.LastLocations);
            HideLoadingScreen();
            return;
        }

        _runtimeBackend.InitialLocationsLoaded -= OnInitialLocationsLoaded;
        _runtimeBackend.InitialLocationsLoaded += OnInitialLocationsLoaded;
        _initialLocationsTimeoutCoroutine = StartCoroutine(HideLoadingAfterInitialLocationsTimeout());
    }

    private void OnInitialLocationsLoaded(List<ZooLocationItem> locations)
    {
        if (_runtimeBackend != null)
            _runtimeBackend.InitialLocationsLoaded -= OnInitialLocationsLoaded;

        if (_initialLocationsTimeoutCoroutine != null)
        {
            StopCoroutine(_initialLocationsTimeoutCoroutine);
            _initialLocationsTimeoutCoroutine = null;
        }

        PrepareOfflineLocalBaseIfNeeded(locations);
        HideLoadingScreen();
    }

    private IEnumerator HideLoadingAfterInitialLocationsTimeout()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, _initialLocationsWaitTimeout));

        if (_runtimeBackend != null)
            _runtimeBackend.InitialLocationsLoaded -= OnInitialLocationsLoaded;

        _initialLocationsTimeoutCoroutine = null;
        Debug.LogWarning($"GameEntryPoint: initial backend locations were not loaded after {_initialLocationsWaitTimeout:0.#}s. Continuing offline.");
        PrepareOfflineLocalBase();
        HideLoadingScreen();
    }

    private void PrepareOfflineLocalBaseIfNeeded(IReadOnlyList<ZooLocationItem> locations)
    {
        if (LobbyClient.Instance != null && LobbyClient.Instance.IsOnline)
            return;

        if (locations != null && locations.Count > 0)
            return;

        PrepareOfflineLocalBase();
    }

    private void PrepareOfflineLocalBase()
    {
        var remoteBases = _remoteBasesApplier != null
            ? _remoteBasesApplier
            : FindAnyObjectByType<RemoteBasesApplier>();

        if (remoteBases != null)
            remoteBases.ApplyOfflineLocalOnly();
    }

    private void HideLoadingScreen()
    {
        if (_loadingHidden)
            return;

        _loadingHidden = true;
        G.GameLoader.ShowLoadingScreen(false);
        AnalyticsManager.Instance.MarkGameReady();
        AnalyticsManager.Instance.MarkGameplayStarted("game_entry");
    }
}

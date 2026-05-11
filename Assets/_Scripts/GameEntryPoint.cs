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

    private ZooBackendClient _runtimeBackend;
    private bool _loadingHidden;

    private void Start()
    {
        Instantiate(_playerManager).Init(_playerSpawnPoint);
        Instantiate(_elements);
        Instantiate(_quickAccess).Init();
        Instantiate(_inventory).Init();
        
        Instantiate(_dailyPlaytimeTracker);
        Instantiate(_hubPrefab).Initialize(G.Currency);
        Instantiate(_ui);
        //Instantiate(_remoteBasesApplier);

        if (G.Backend == null && _backend != null)
            Instantiate(_backend);
        LobbyDebugPanel.EnsureExists();

        G.Initialized?.Invoke();
        //_scene.SetActive(true);
        WaitForPlayerLocationsBeforeHideLoading();
    }

    private void OnDestroy()
    {
        if (_runtimeBackend != null)
            _runtimeBackend.InitialLocationsLoaded -= OnInitialLocationsLoaded;
    }

    private void WaitForPlayerLocationsBeforeHideLoading()
    {
        _runtimeBackend = G.Backend;
        if (_runtimeBackend == null)
        {
            HideLoadingScreen();
            return;
        }

        if (_runtimeBackend.IsInitialLocationsLoaded)
        {
            HideLoadingScreen();
            return;
        }

        _runtimeBackend.InitialLocationsLoaded -= OnInitialLocationsLoaded;
        _runtimeBackend.InitialLocationsLoaded += OnInitialLocationsLoaded;
    }

    private void OnInitialLocationsLoaded(System.Collections.Generic.List<ZooLocationItem> _)
    {
        if (_runtimeBackend != null)
            _runtimeBackend.InitialLocationsLoaded -= OnInitialLocationsLoaded;

        HideLoadingScreen();
    }

    private void HideLoadingScreen()
    {
        if (_loadingHidden)
            return;

        _loadingHidden = true;
        G.GameLoader.ShowLoadingScreen(false);
    }
}

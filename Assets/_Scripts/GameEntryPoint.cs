using UnityEngine;

public class GameEntryPoint : MonoBehaviour
{
    [SerializeField] private Inventory _inventory;
    [SerializeField] private PlayerManager _playerManager;
    [SerializeField] private QuickAccessManager _quickAccess;
    [SerializeField] private GameObject _ui;
    [SerializeField] private GameObject _scene;
    [SerializeField] private ElementTypeMultiplaer _elements;

    [SerializeField] private IncomeModifiersHub _hubPrefab;
    [SerializeField] private DailyPlaytimeTrackerMB _dailyPlaytimeTracker;

    [SerializeField] private Transform _playerSpawnPoint;

    private void Start()
    {
        Instantiate(_playerManager).Init(_playerSpawnPoint);
        Instantiate(_elements);
        Instantiate(_quickAccess).Init();
        Instantiate(_inventory).Init();
        
        Instantiate(_dailyPlaytimeTracker);
        Instantiate(_hubPrefab).Initialize(G.Currency);
        Instantiate(_ui);

        G.Initialized?.Invoke();
        //_scene.SetActive(true);
        G.GameLoader.ShowLoadingScreen(false);
    }
}

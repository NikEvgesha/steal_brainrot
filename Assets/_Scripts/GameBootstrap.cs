using System;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameLoader _gameLoader;
    [SerializeField] private SoundManager _soundManager;
    [SerializeField] private SaveManager _saveManager;
    [SerializeField] private LocalizationManager _localizationManager;
    [SerializeField] private Settings _settings;
    [SerializeField] private PlayerInput _input;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private ControlManager _controlManager;
    [SerializeField] private CurrencyManager _cuurencyManager;
    [SerializeField] private PurchasesManager _purchaseManager;
    [SerializeField] private AdsManager _adsManager;
    [SerializeField] private ItemPrefabStorage _storage;
    [SerializeField] private ZooBackendClient _backend;
    [SerializeField] private LobbyClient _lobby;

    [SerializeField] private GameScene _gameScene;


    private void Start()
    {
        Instantiate(_gameLoader);
        G.GameLoader.ShowLoadingScreen(true);


        Instantiate(_soundManager);
        Instantiate(_saveManager);
        Instantiate(_localizationManager);
        Instantiate(_settings);
        Instantiate(_input);
        Instantiate(_gameManager);
        Instantiate(_controlManager);
        Instantiate(_cuurencyManager);
        Instantiate(_adsManager);
        Instantiate(_purchaseManager);
        Instantiate(_storage);
        Instantiate(_backend);
        Instantiate(_lobby);

        new GameObject("GiftInboxUI").AddComponent<GiftInboxUI>();
        TryCreateRuntimeUi("FriendRequestInboxUI", "FriendRequestInboxUI");
        new GameObject("LobbyDebugPanel").AddComponent<LobbyDebugPanel>();

        G.GameLoader.LoadNextScene(_gameScene.ToString(), false);
    }

    private static void TryCreateRuntimeUi(string objectName, string typeName)
    {
        var type = Type.GetType(typeName + ", Assembly-CSharp");
        if (type == null)
        {
            Debug.LogWarning($"[GameBootstrap] Runtime UI type not found: {typeName}");
            return;
        }

        var go = new GameObject(objectName);
        go.AddComponent(type);
    }

}

using System.Collections;
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


    private IEnumerator Start()
    {
        _ = AnalyticsManager.Instance;
        LocalizationManager localization = Instantiate(_localizationManager);
        Instantiate(_gameLoader);
        G.GameLoader.ShowLoadingScreen(true);


        Instantiate(_soundManager);
        Instantiate(_saveManager);
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
        _ = OfflineRewardManager.Instance;
        new GameObject("AlbumProgressService").AddComponent<AlbumProgressService>();

        new GameObject("GiftInboxUI").AddComponent<GiftInboxUI>();
        new GameObject("FriendRequestInboxUI").AddComponent<FriendRequestInboxUI>();

        // Wait for the actual Mirra platform language before leaving the
        // bootstrap scene. This gives the localized loading labels a rendered
        // frame instead of updating them during a synchronous scene change.
        while (localization != null && !localization.IsLanguageReady)
            yield return null;

        // Keep the bootstrap loading screen in front until the startup ad has
        // either closed or exhausted its readiness timeout. This prevents the
        // first interstitial from appearing over the already running world.
        while (G.Ad != null && !G.Ad.StartupInterstitialFinished)
            yield return null;

        Canvas.ForceUpdateCanvases();
        yield return null;

        G.GameLoader.LoadNextScene(_gameScene.ToString(), false);
    }

}

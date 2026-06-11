using MirraGames.SDK;
using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1)]
public class LoadingManager : MonoBehaviour
{
    [SceneSelector]
    [SerializeField] private string _lobbyScene;
    [SceneSelector]
    [SerializeField] private string _gameScene;

    private Location _location = Location.None;
    public Location CurrentLocation;

    public Action<Location> LocationChanged;

    private static LoadingManager _instance;
    public static LoadingManager Instance { get { return _instance; } }


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("LoadingManager уже существует! Удаляем дубликат.");
            Destroy(gameObject);
        }
    }
    void Start()
    {
        G.GameLoader.OnSceneLoaded += OnSceneLoaded;
        MirraSDK.WaitForProviders(static () => {
            LoadingManager.Instance.StartGame();
            // Методы SDK не должны вызывать вылет или NullReferenceException,
            // делегат будет вызван только когда все провайдеры имеют статус IsInitialized.
        });

    }
    private void StartGame()
    {
        G.GameLoader.StartAfterSDK();
        bool haveSave = false;  //SaveManager.Instance.LoadGameProgress().Item1 >= 0;
        if (G.Save.IsNewPlayer || haveSave)
        {
            _location = Location.Game;
            if (!haveSave)
                G.GameLoader.LoadNextScene(_gameScene, true);
            else
            {
                // Load saved game    
            }
           
            G.Save.SetSave(true);
        }
        else
        {
            G.GameLoader.LoadNextScene(_lobbyScene, true);
            _location = Location.Lobby;
        }
    }

    private void OnDisable()
    {
        G.GameLoader.OnSceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded()
    {
        CurrentLocation = _location;
        LocationChanged?.Invoke(CurrentLocation);
        MirraSDK.Analytics.GameplayStart();
        //Debug.Log("GameplayStart");
        
    }

    public void LoadLocation(Location location, string sceneName = null, bool withAds = true)
    {
        MirraSDK.Analytics.GameplayStop();
        //Debug.Log("GameplayStop");
        if (location == Location.Game)
        {
            G.GameLoader.LoadNextScene(sceneName != null ? sceneName : _gameScene, true);
            _location = Location.Game;
        }
        else if (location == Location.Lobby)
        {
            G.GameLoader.LoadNextScene(_lobbyScene, true, withAds);
            _location = Location.Lobby;
        }
    }

}

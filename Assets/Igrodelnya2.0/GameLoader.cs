using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameLoader : MonoBehaviour
{

    [SerializeField] private GameObject _loadingImage;
    private string _currentSceneName;
    private AsyncOperation _asyncOperation;

    private bool _startLoadingFinished = false;

    public Action OnSceneLoaded;


    private void Awake()
    {
        if (G.GameLoader == null)
        {
            G.GameLoader = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("GameLoader уже существует! Удаляем дубликат.");
            Destroy(gameObject);
        }
        
    }


    public void StartAfterSDK()
    {
        //_currentSceneName = _gameOptions.LobbySceneName;
        //SceneManager.LoadScene(_currentSceneName);
        if (PurchasesManager.Instance.PurchasesAvailable())
        {
            _startLoadingFinished = true;
        }
    }

    public void LoadNextScene(string SceneName, bool asyncMode,bool withAds = true)
    {
        _currentSceneName = SceneName;

        if (asyncMode)
        {
            if (_startLoadingFinished && withAds)
                AdsManager.Instance.ShowInterstitialAd();
            else
                _startLoadingFinished = true;

            _loadingImage.SetActive(true);
            StartCoroutine("SceneLoad", _currentSceneName);
            //AdsManager.Instance.ShowInterstitialAd();
        } else
        {
            SceneManager.LoadScene(_currentSceneName);
        }

        
    }


    private IEnumerator SceneLoad(string sceneName)
    {
        float loadingProgress;
        _asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        while (_asyncOperation.progress < 0.95f)
        {
            loadingProgress = Mathf.Clamp01(_asyncOperation.progress / 0.95f);
            LoadingProgressBarUI.Instance?.Progress(_asyncOperation.progress);
            yield return true;
        }

        if (!PauseManager.Instance.IsInitialize)
            PauseManager.Instance.StartInitialize();

        _loadingImage.SetActive(false);
        //AdsManager.Instance.ShowInterstitialAd();
        OnSceneLoaded?.Invoke();
    }

    public void ShowLoadingScreen(bool show) { _loadingImage.SetActive(show); }

}

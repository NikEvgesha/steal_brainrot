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
        if (G.Purchases != null && G.Purchases.PurchasesAvailable())
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
                G.Ad.ShowInterstitialAd();
            else
                _startLoadingFinished = true;

            if (_loadingImage != null) _loadingImage.SetActive(true);
            StartCoroutine("SceneLoad", _currentSceneName);
            // G.Ad.ShowInterstitialAd();
        }
        else
        {
            SceneManager.LoadScene(_currentSceneName);
        }

        
    }


    private IEnumerator SceneLoad(string sceneName)
    {
        float loadingProgress;

        yield return null;

        _asyncOperation = SceneManager.LoadSceneAsync(sceneName);
        while (_asyncOperation.progress < 0.95f)
        {
            loadingProgress = Mathf.Clamp01(_asyncOperation.progress / 0.95f);
            LoadingProgressBarUI.Instance?.Progress(loadingProgress);
            yield return null;
        }

        if (!PauseManager.Instance.IsInitialize)
            PauseManager.Instance.StartInitialize();

        if (LoadingProgressBarUI.Instance != null)
        {
            LoadingProgressBarUI.Instance.EndProgress(0.25f);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        if (_loadingImage != null) _loadingImage.SetActive(false);
        //G.Ad.ShowInterstitialAd();
        OnSceneLoaded?.Invoke();
    }

    public void ShowLoadingScreen(bool show) { if (_loadingImage != null) _loadingImage.SetActive(show); }

}

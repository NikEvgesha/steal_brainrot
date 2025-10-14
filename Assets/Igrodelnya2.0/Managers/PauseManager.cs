using MirraGames.SDK.Common;
using UnityEngine;

/// <summary>
/// Синглтон-менеджер паузы, кидающий всё на выбранный PauseProvider.
/// </summary>
public class PauseManager : MonoBehaviour
{
    [Tooltip("Выберите провайдер паузы (DefaultPauseProvider или MirraSDKPauseProvider)")]
    [SerializeField] private PauseProvider _provider;

    private static PauseManager _instance;
    public static PauseManager Instance => _instance;

    private bool _isInitialize;
    public bool IsInitialize
    {
        get { return _isInitialize; }
        set {}
    }

    public bool IsPaused => _provider != null && _provider.IsPaused;
    private int _pause = 0;
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            //DontDestroyOnLoad(gameObject);

            if (_provider == null)
                _provider = GetComponent<PauseProvider>();

            if (_provider == null)
                Debug.LogError("PauseManager: не назначен PauseProvider!");

            
            _provider.OnPauseChanged += OnProviderPauseChanged;
        }
        else
        {
            Destroy(gameObject);
        }
    }


    private void Start()
    {
        AdsManager.Instance.AdClosed += OnAdClosed;
    }

    public void StartInitialize()
    {
        _isInitialize = true;
        _provider?.Initialize();
    }
    private void OnProviderPauseChanged(bool isPaused)
    {
        // Здесь можно доп. логику по смене UI и т.п.
    }

    /// <summary>
    /// Ставим/снимаем паузу через провайдер.
    /// </summary>
    public void SetPause(bool paused, bool controlAudio = true)
    {
        _pause += paused ? 1 : -1;

        if (_pause < 0)
            _pause = 0;

        _provider?.SetPause(_pause != 0, controlAudio);
    }

    private void OnDestroy()
    {
        AdsManager.Instance.AdClosed -= OnAdClosed;
        if (_provider != null)
            _provider.OnPauseChanged -= OnProviderPauseChanged;
    }

    private void OnAdClosed()
    {
        SetPause(false, true);
    }

}
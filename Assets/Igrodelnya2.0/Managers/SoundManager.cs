using System;
using UnityEngine;
using UnityEngine.Audio;
using MirraGames.SDK;  // MirraSDK audio API

public class SoundManager : MonoBehaviour
{
    private const float minVolume = -80f;
    private const float maxVolume = 0f;

    [SerializeField] private AudioMixerGroup _mixer;
    [SerializeField] private AnimationCurve _curve;
    [SerializeField] private AudioSource _music;
    [SerializeField] private AudioSource _uiClick;
    [SerializeField] private AudioClip _lose;
    [SerializeField] private AudioClip _win;

    private float _soundVolume;
    private float _musicVolume;

    // Названия параметров в AudioMixer
    private readonly string _soundName = "SoundVolume";
    private readonly string _musicName = "MusicVolume";

    public bool IsReady { get; private set; }
    public event Action Ready;

    /// <summary>
    /// Глобальный мастер-громкость через MirraSDK
    /// </summary>
    public float MasterVolume
    {
        get => MirraSDK.Audio.Volume;
        set
        {
            MirraSDK.Audio.Volume = Mathf.Clamp01(value);
            //Debug.Log($"Master volume set to: {MirraSDK.Audio.Volume}");
        }
    }

    /// <summary>
    /// Громкость звуковых эффектов (SFX)
    /// </summary>
    public float SoundVolume
    {
        get => _soundVolume;
        set
        {
            _soundVolume = Mathf.Clamp01(value);
            // Применяем к группе SFX
            float db = Mathf.Lerp(minVolume, maxVolume, _curve.Evaluate(_soundVolume));
            _mixer.audioMixer.SetFloat(_soundName, db);
            // Обновляем глобальную громкость
            //MasterVolume = _soundVolume;
            G.SaveManager.SaveSoundVolume(_soundVolume);
            //Debug.Log($"Sound SFX volume set to: {_soundVolume} (db={db})");
        }
    }

    /// <summary>
    /// Громкость фоновой музыки
    /// </summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Mathf.Clamp01(value);
            float db = Mathf.Lerp(minVolume, maxVolume, _curve.Evaluate(_musicVolume));
            _mixer.audioMixer.SetFloat(_musicName, db);
            //MasterVolume = _musicVolume;
            G.SaveManager.SaveMusicVolume(_musicVolume);
            //Debug.Log($"Music volume set to: {_musicVolume} (db={db})");
        }
    }

    private void Awake()
    {
        if (G.SoundManager == null)
        {
            G.SoundManager = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    private void Start()
    {
        MirraSDK.WaitForProviders(static () => {
            G.SoundManager.StartGame();
            // Методы SDK не должны вызывать вылет или NullReferenceException,
            // делегат будет вызван только когда все провайдеры имеют статус IsInitialized.
        });
    }
    private void StartGame()
    {
        // Загружаем сохранённые параметры
        float[] volume = G.SaveManager.GetVolume();
        MusicVolume = volume[0];
        SoundVolume = volume[1];

        IsReady = true;
        Ready?.Invoke();
        PlayMusicLoop();
    }
    public void OnPauseAudioChanged(bool paused)
    {
        // Управление звуком через MirraSDK при паузе
        MirraSDK.Audio.Pause = paused; 
    }

    public void PlayUIClick()
    {
        if (_uiClick != null)
            _uiClick.Play();
    }

    public void PlayMusicLoop()
    {
        if (_music != null)
        {
            _music.loop = true;
            _music.Play();
        }
    }

    public void PlayLose()
    {
        if (_music != null && _lose != null)
        {
            _music.loop = false;
            _music.clip = _lose;
            _music.Play();
        }
    }

    public void PlayWin()
    {
        if (_music != null && _win != null)
        {
            _music.loop = false;
            _music.clip = _win;
            _music.Play();
        }
    }
}

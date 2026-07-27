using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MirraGames.SDK;  // MirraSDK audio API

public class SoundManager : MonoBehaviour
{
    private const float minVolume = -80f;
    private const float maxVolume = 0f;
    private const string EventAudioResourcePath = "Audio/GDC2026";

    [SerializeField] private AudioMixerGroup _mixer;
    [SerializeField] private AnimationCurve _curve;
    [SerializeField] private AudioSource _music;
    [SerializeField] private AudioSource _uiClick;
    [SerializeField] private AudioClip _lose;
    [SerializeField] private AudioClip _win;
    [Header("Event audio")]
    [SerializeField] private bool _enableEventAudio = true;
    [SerializeField] private bool _playZooAmbience = true;
    [SerializeField, Min(8)] private int _oneShotPoolSize = 24;

    private float _soundVolume;
    private float _musicVolume;
    private readonly Dictionary<string, AudioClip> _eventClips = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GameAudioId, float> _lastPlayedAt = new();
    private readonly List<AudioSource> _oneShotSources = new();
    private readonly List<RaycastResult> _uiRaycastResults = new(12);
    private readonly Dictionary<GameAudioBus, AudioMixerGroup> _busGroups = new();
    private PointerEventData _pointerEventData;
    private AudioSource _zooAmbience;

    // Названия параметров в AudioMixer
    private readonly string _soundName = "SoundVolume";
    private readonly string _musicName = "MusicVolume";

    public bool IsReady { get; private set; }
    public int LoadedEventClipCount => _eventClips.Count;
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
            G.Save.SaveSoundVolume(_soundVolume);
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
            G.Save.SaveMusicVolume(_musicVolume);
            //Debug.Log($"Music volume set to: {_musicVolume} (db={db})");
        }
    }

    private void Awake()
    {
        if (G.Sound == null)
        {
            G.Sound = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadEventAudio();
    }

    private void Start()
    {
        MirraSDK.WaitForProviders(static () => {
            G.Sound.StartGame();
            // Методы SDK не должны вызывать вылет или NullReferenceException,
            // делегат будет вызван только когда все провайдеры имеют статус IsInitialized.
        });
    }
    private void StartGame()
    {
        // Загружаем сохранённые параметры
        float[] volume = G.Save.GetVolume();
        MusicVolume = volume[0];
        SoundVolume = volume[1];

        IsReady = true;
        Ready?.Invoke();
        PlayMusicLoop();

        if (_playZooAmbience && _zooAmbience == null)
            _zooAmbience = PlayLoop(GameAudioId.AMB_ZOO_DAY, null, 1f);
    }

    private void Update()
    {
        if (!_enableEventAudio || EventSystem.current == null)
            return;

        if (Input.GetMouseButtonDown(0))
            TryPlayPointerUiSound(Input.mousePosition);

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
                TryPlayPointerUiSound(touch.position);
        }
    }

    private void OnDestroy()
    {
        if (G.Sound == this)
            G.Sound = null;
    }

    public void OnPauseAudioChanged(bool paused)
    {
        // Управление звуком через MirraSDK при паузе
        MirraSDK.Audio.Pause = paused; 
    }

    public void PlayUIClick()
    {
        if (!Play(GameAudioId.SFX_UI_CLICK) && _uiClick != null)
            _uiClick.Play();
    }

    public bool Play(
        GameAudioId id,
        Transform emitter = null,
        float volumeScale = 1f,
        float pitchScale = 1f)
    {
        Vector3? position = emitter != null ? emitter.position : null;
        return PlayInternal(id, position, volumeScale, pitchScale);
    }

    public bool PlayAt(
        GameAudioId id,
        Vector3 position,
        float volumeScale = 1f,
        float pitchScale = 1f)
    {
        return PlayInternal(id, position, volumeScale, pitchScale);
    }

    public AudioSource PlayLoop(GameAudioId id, Transform emitter = null, float volumeScale = 1f)
    {
        if (!_enableEventAudio ||
            !GameAudioCatalog.TryGet(id, out GameAudioCueDefinition cue) ||
            !cue.Loop ||
            cue.ClipNames == null ||
            cue.ClipNames.Length == 0)
        {
            return null;
        }

        AudioClip clip = ResolveClip(cue.ClipNames[0]);
        if (clip == null)
            return null;

        var loopObject = new GameObject("AudioLoop_" + id);
        if (emitter != null)
        {
            loopObject.transform.SetParent(emitter, false);
            loopObject.transform.localPosition = Vector3.zero;
        }
        else
        {
            loopObject.transform.SetParent(transform, false);
        }

        AudioSource source = loopObject.AddComponent<AudioSource>();
        ConfigureSource(source, cue, emitter != null ? emitter.position : transform.position);
        source.clip = clip;
        source.loop = true;
        source.volume = Mathf.Clamp01(cue.Volume * volumeScale);
        source.pitch = UnityEngine.Random.Range(cue.PitchMin, cue.PitchMax);
        source.Play();
        return source;
    }

    public void StopLoop(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        if (source.gameObject != gameObject &&
            (source.transform.parent == transform || source.gameObject.name.StartsWith("AudioLoop_", StringComparison.Ordinal)))
        {
            Destroy(source.gameObject);
        }
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

    private bool PlayInternal(
        GameAudioId id,
        Vector3? position,
        float volumeScale,
        float pitchScale)
    {
        if (!_enableEventAudio || !GameAudioCatalog.TryGet(id, out GameAudioCueDefinition cue))
            return false;

        float now = Time.unscaledTime;
        if (cue.MinInterval > 0f &&
            _lastPlayedAt.TryGetValue(id, out float lastPlayedAt) &&
            now - lastPlayedAt < cue.MinInterval)
        {
            return false;
        }

        if (cue.ClipNames == null || cue.ClipNames.Length == 0)
            return false;

        bool played = false;
        if (cue.Layered)
        {
            for (int i = 0; i < cue.ClipNames.Length; i++)
                played |= PlayClip(cue, cue.ClipNames[i], position, volumeScale, pitchScale);
        }
        else
        {
            int index = cue.ClipNames.Length == 1 ? 0 : UnityEngine.Random.Range(0, cue.ClipNames.Length);
            played = PlayClip(cue, cue.ClipNames[index], position, volumeScale, pitchScale);
        }

        if (played)
            _lastPlayedAt[id] = now;
        return played;
    }

    private bool PlayClip(
        GameAudioCueDefinition cue,
        string clipName,
        Vector3? position,
        float volumeScale,
        float pitchScale)
    {
        AudioClip clip = ResolveClip(clipName);
        AudioSource source = AcquireOneShotSource();
        if (clip == null || source == null)
            return false;

        Vector3 sourcePosition = position ?? transform.position;
        ConfigureSource(source, cue, sourcePosition);
        source.clip = clip;
        source.loop = false;
        source.volume = Mathf.Clamp01(cue.Volume * volumeScale);
        source.pitch = Mathf.Clamp(
            UnityEngine.Random.Range(cue.PitchMin, cue.PitchMax) * Mathf.Max(0.01f, pitchScale),
            0.35f,
            3f);
        source.Play();
        return true;
    }

    private AudioSource AcquireOneShotSource()
    {
        for (int i = 0; i < _oneShotSources.Count; i++)
        {
            AudioSource source = _oneShotSources[i];
            if (source != null && !source.isPlaying)
                return source;
        }

        if (_oneShotSources.Count >= Mathf.Max(8, _oneShotPoolSize))
            return null;

        var sourceObject = new GameObject($"EventAudio_{_oneShotSources.Count:00}");
        sourceObject.transform.SetParent(transform, false);
        AudioSource created = sourceObject.AddComponent<AudioSource>();
        created.playOnAwake = false;
        created.loop = false;
        created.dopplerLevel = 0f;
        _oneShotSources.Add(created);
        return created;
    }

    private void ConfigureSource(AudioSource source, GameAudioCueDefinition cue, Vector3 position)
    {
        source.transform.position = position;
        source.outputAudioMixerGroup = ResolveMixerGroup(cue.Bus);
        source.spatialBlend = cue.Spatial ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = cue.MinDistance;
        source.maxDistance = cue.MaxDistance;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;
    }

    private AudioMixerGroup ResolveMixerGroup(GameAudioBus bus)
    {
        if (_busGroups.TryGetValue(bus, out AudioMixerGroup cached))
            return cached;
        if (_mixer == null || _mixer.audioMixer == null)
            return null;

        string groupName = bus switch
        {
            GameAudioBus.UI => "UI",
            GameAudioBus.Environment => "Environment",
            _ => "Sound"
        };
        AudioMixerGroup[] matches = _mixer.audioMixer.FindMatchingGroups(groupName);
        AudioMixerGroup resolved = matches != null && matches.Length > 0 ? matches[0] : _mixer;
        _busGroups[bus] = resolved;
        return resolved;
    }

    private AudioClip ResolveClip(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return null;
        _eventClips.TryGetValue(clipName, out AudioClip clip);
        return clip;
    }

    private void LoadEventAudio()
    {
        _eventClips.Clear();
        if (!_enableEventAudio)
            return;

        AudioClip[] clips = Resources.LoadAll<AudioClip>(EventAudioResourcePath);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip != null && !_eventClips.ContainsKey(clip.name))
                _eventClips.Add(clip.name, clip);
        }

        if (_eventClips.Count == 0)
            Debug.LogWarning($"[SoundManager] No event clips found in Resources/{EventAudioResourcePath}.");
    }

    private void TryPlayPointerUiSound(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        _pointerEventData ??= new PointerEventData(eventSystem);
        _pointerEventData.position = screenPosition;
        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);

        for (int i = 0; i < _uiRaycastResults.Count; i++)
        {
            GameObject hit = _uiRaycastResults[i].gameObject;
            if (hit == null)
                continue;

            Slider slider = hit.GetComponentInParent<Slider>();
            if (slider != null && slider.interactable)
            {
                Play(GameAudioId.SFX_UI_SLIDER);
                return;
            }

            Toggle toggle = hit.GetComponentInParent<Toggle>();
            if (toggle != null && toggle.interactable)
            {
                Play(GameAudioId.SFX_UI_TOGGLE);
                return;
            }

            Button button = hit.GetComponentInParent<Button>();
            if (button == null)
                continue;
            if (button.GetComponent<UISound>() != null)
                return;

            Play(button.interactable ? GameAudioId.SFX_UI_CLICK : GameAudioId.SFX_UI_LOCKED);
            return;
        }
    }
}

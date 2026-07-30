using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingUI : MonoBehaviour
{
    [SerializeField] private Scrollbar _musicVolume;
    [SerializeField] private Scrollbar _soundVolume;
    [SerializeField] private Scrollbar _sensivity;
    [SerializeField] private Toggle _animationsToggle;
    [SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _exitButton;
    [SerializeField] private GameObject _lobbyButtons;

    private bool _isOpen;
    private readonly Dictionary<string, PendingSetting> _pendingSettings = new();

    private void Update()
    {
        if (_pendingSettings.Count == 0)
            return;

        float now = Time.unscaledTime;
        var ready = new List<string>();
        foreach (KeyValuePair<string, PendingSetting> pair in _pendingSettings)
        {
            if (now - pair.Value.ChangedAt >= 1f)
                ready.Add(pair.Key);
        }
        for (int i = 0; i < ready.Count; i++)
            FlushSetting(ready[i]);
    }

    public void ToggleOpen()
    {
        bool wasOpen = _isOpen;
        _isOpen = !_isOpen;
        //_lobbyButtons.SetActive(G.Game);
        if (!G.Control.UseTouchControl)
            G.Control.CursorActive = _isOpen;
        _panel.SetActive(_isOpen);
        //_exitButton.SetActive(_isOpen);
        //PauseManager.Instance.SetPause(_isOpen, false);
        G.IsPaused = _isOpen;


        if (_isOpen)
            G.Input.AOpenWindow?.Invoke(this);
        else if (wasOpen)
            FlushAllSettings();
        
    }
    private void Start()
    {
        G.Input.APause += ToggleOpen;
        ResolveAnimationsToggle();
        if (_animationsToggle != null)
        {
            _animationsToggle.onValueChanged.RemoveListener(OnAnimationsToggleChange);
            _animationsToggle.onValueChanged.AddListener(OnAnimationsToggleChange);
        }

        if (G.Sound.IsReady)
        {
            SetValues();
        } else
        {
            G.Sound.Ready += SetValues;
        }

        if (G.Settings.IsReady)
        {
            SetSensivity();
        }
        else
        {
            G.Settings.Ready += SetSensivity;
        }

    }

    private void OnDisable()
    {
        if (G.Sound != null)
            G.Sound.Ready -= SetValues;
        if (G.Input != null)
            G.Input.APause -= ToggleOpen;
        if (_animationsToggle != null)
            _animationsToggle.onValueChanged.RemoveListener(OnAnimationsToggleChange);
    }

    private void SetValues()
    {
        _musicVolume.value = G.Sound.MusicVolume;
        _soundVolume.value = G.Sound.SoundVolume;
    }

    private void SetSensivity()
    {
        _sensivity.value = G.Settings.Sensivity;
        if (_animationsToggle != null)
            _animationsToggle.SetIsOnWithoutNotify(G.Settings.AnimalsAnimationsEnabled);
    }


    public void OnMusicVolumeChange(float value)
    {
        float before = G.Sound != null ? G.Sound.MusicVolume : value;
        G.Settings.MusicVolume(value);
        if (_isOpen)
            MarkSettingChanged("music_volume", before, value);
    }

    public void OnSoundVolumeChange(float value)
    {
        float before = G.Sound != null ? G.Sound.SoundVolume : value;
        G.Settings.SoundVolume(value);
        if (_isOpen)
            MarkSettingChanged("sound_volume", before, value);
    }

    public void OnSensivityChange(float value)
    {
        float before = G.Settings != null ? G.Settings.Sensivity : value;
        G.Settings.Sensitivity(value);
        if (_isOpen)
            MarkSettingChanged("sensitivity", before, value);
    }

    public void OnAnimationsToggleChange(bool isEnabled)
    {
        bool before = G.Settings != null && G.Settings.AnimalsAnimationsEnabled;
        G.Settings.SetAnimalsAnimationsEnabled(isEnabled);
        if (_isOpen && before != isEnabled)
        {
            MarkSettingChanged("animal_animations", before, isEnabled);
            FlushSetting("animal_animations");
        }
    }

    private void MarkSettingChanged(string name, object before, object after)
    {
        if (_pendingSettings.TryGetValue(name, out PendingSetting pending))
        {
            pending.After = after;
            pending.ChangedAt = Time.unscaledTime;
            return;
        }

        _pendingSettings[name] = new PendingSetting
        {
            Before = before,
            After = after,
            ChangedAt = Time.unscaledTime
        };
    }

    private void FlushAllSettings()
    {
        if (_pendingSettings.Count == 0)
            return;
        var names = new List<string>(_pendingSettings.Keys);
        for (int i = 0; i < names.Count; i++)
            FlushSetting(names[i]);
    }

    private void FlushSetting(string name)
    {
        if (!_pendingSettings.TryGetValue(name, out PendingSetting pending))
            return;
        _pendingSettings.Remove(name);
        GameAnalytics.Track(AnalyticsEventNames.SettingsChanged, GameAnalytics.Params(
            "setting_name", name,
            "value_before", pending.Before,
            "value_after", pending.After,
            "source", "settings_panel",
            "result", "changed"),
            AnalyticsPriority.Normal,
            name);
    }

    private sealed class PendingSetting
    {
        public object Before;
        public object After;
        public float ChangedAt;
    }

    private void ResolveAnimationsToggle()
    {
        if (_animationsToggle != null)
            return;

        var toggles = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            var candidate = toggles[i];
            if (candidate == null)
                continue;

            var name = candidate.name;
            if (!string.IsNullOrWhiteSpace(name) &&
                name.IndexOf("animation", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _animationsToggle = candidate;
                return;
            }
        }

        if (toggles.Length > 0)
            _animationsToggle = toggles[0];
    }


}

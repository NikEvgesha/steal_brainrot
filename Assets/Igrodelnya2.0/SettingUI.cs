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
    public void ToggleOpen()
    {
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
        G.Settings.MusicVolume(value);
    }

    public void OnSoundVolumeChange(float value)
    {
        G.Settings.SoundVolume(value);
    }

    public void OnSensivityChange(float value)
    {
        G.Settings.Sensitivity(value);
    }

    public void OnAnimationsToggleChange(bool isEnabled)
    {
        G.Settings.SetAnimalsAnimationsEnabled(isEnabled);
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

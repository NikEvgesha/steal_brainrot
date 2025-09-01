using UnityEngine;
using UnityEngine.UI;

public class SettingUI : MonoBehaviour
{
    [SerializeField] private Scrollbar _musicVolume;
    [SerializeField] private Scrollbar _soundVolume;
    [SerializeField] private Scrollbar _sensivity;
    [SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _exitButton;
    [SerializeField] private GameObject _lobbyButtons;

    private bool _isOpen;
    public void ToggleOpen()
    {
        _isOpen = !_isOpen;
        //_lobbyButtons.SetActive(GameManager.Instance);
        if (!ControlManager.Instance.UseTouchControl)
            ControlManager.Instance.CursorActive = _isOpen;
        _panel.SetActive(_isOpen);
        //_exitButton.SetActive(_isOpen);
        PauseManager.Instance.SetPause(_isOpen, false);
        
        if (_isOpen)
            PlayerInput.Instance.AOpenWindow?.Invoke(this);
        
    }
    private void Start()
    {
        PlayerInput.Instance.APause += ToggleOpen;
        if (SoundManager.Instance.IsReady)
        {
            SetValues();
        } else
        {
            SoundManager.Instance.Ready += SetValues;
        }

        if (Settings.instance.IsReady)
        {
            SetSensivity();
        }
        else
        {
            Settings.instance.Ready += SetSensivity;
        }

    }

    private void OnDisable()
    {
        SoundManager.Instance.Ready -= SetValues;
        PlayerInput.Instance.APause -= ToggleOpen;
    }

    private void SetValues()
    {
        _musicVolume.value = SoundManager.Instance.MusicVolume;
        _soundVolume.value = SoundManager.Instance.SoundVolume;
    }

    private void SetSensivity()
    {
        _sensivity.value = Settings.instance.Sensivity;
    }
}

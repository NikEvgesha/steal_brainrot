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
        //_lobbyButtons.SetActive(G.Game);
        if (!G.Control.UseTouchControl)
            G.Control.CursorActive = _isOpen;
        _panel.SetActive(_isOpen);
        //_exitButton.SetActive(_isOpen);
        PauseManager.Instance.SetPause(_isOpen, false);
        
        if (_isOpen)
            G.Input.AOpenWindow?.Invoke(this);
        
    }
    private void Start()
    {
        G.Input.APause += ToggleOpen;
        if (G.SoundManager.IsReady)
        {
            SetValues();
        } else
        {
            G.SoundManager.Ready += SetValues;
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
        G.SoundManager.Ready -= SetValues;
        G.Input.APause -= ToggleOpen;
    }

    private void SetValues()
    {
        _musicVolume.value = G.SoundManager.MusicVolume;
        _soundVolume.value = G.SoundManager.SoundVolume;
    }

    private void SetSensivity()
    {
        _sensivity.value = G.Settings.Sensivity;
    }
}

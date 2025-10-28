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
        G.Sound.Ready -= SetValues;
        G.Input.APause -= ToggleOpen;
    }

    private void SetValues()
    {
        _musicVolume.value = G.Sound.MusicVolume;
        _soundVolume.value = G.Sound.SoundVolume;
    }

    private void SetSensivity()
    {
        _sensivity.value = G.Settings.Sensivity;
    }
}

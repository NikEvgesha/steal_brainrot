using MirraGames.SDK;
using System;
using UnityEngine;

public class Settings : MonoBehaviour
{
    public Action<float> ChangeMouseSensitivity;
    public Action<float> ChangeVolume;
    //public Action<bool> ControllJostick;
    //[SerializeField] private Toggle _joystickToggle;
    //[SerializeField] private GameObject _UIWindow;
    //[SerializeField] private GameObject _exitButton;

    public bool IsReady;
    public Action Ready;

    public float Sensivity;

    //private bool _isOpen;

    private void Awake()
    {

        if (G.Settings == null)
        {
            G.Settings = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


    /*    private void OnEnable()
        {
            UIInputHandler.Instance.SettingaOpenAction += ToggleUIOpen;
            _isOpen = false;
        }

        private void OnDisable()
        {
            UIInputHandler.Instance.SettingaOpenAction -= ToggleUIOpen;
        }*/

    private void OnEnable()
    {
        //G.Game.LevelInProgress += OnLevelSwitch;
    }

    private void OnDisable()
    {
        //G.Game.LevelInProgress -= OnLevelSwitch;
    }

    private void Start()
    {
        
        MirraSDK.WaitForProviders(static () => {
            G.Settings.OnGameStart();
            // Методы SDK не должны вызывать вылет или NullReferenceException,
            // делегат будет вызван только когда все провайдеры имеют статус IsInitialized.
        });
    }

    private void OnGameStart()
    {
        Sensitivity(G.Save.LoadSensivity());
        IsReady = true;
        Ready?.Invoke();
    }

    //private void OnLevelSwitch(bool inProgress)
    //{
    //    if (_exitButton != null)
    //        _exitButton.gameObject.SetActive(inProgress);
    //}


   
    public void Sensitivity(float sens)
    {
        Sensivity = sens;
        ChangeMouseSensitivity?.Invoke(sens);
        G.Save.SaveSensivity(sens);
    }

    public void SoundVolume(float volume)
    {
        G.Sound.SoundVolume = volume;
    }
    public void MusicVolume(float volume)
    {
        G.Sound.MusicVolume = volume;
    }

/*    private void ToggleUIOpen()
    {
        _isOpen = !_isOpen;
        _UIWindow.SetActive(_isOpen);
        if (G.Control && G.Control.UseCursor != _isOpen)
        {
            G.Control.UnlockMouse();
        }
    }*/

}

using MirraGames.SDK;
using System;
using Unity.VisualScripting;
using UnityEngine;

public class Settings : MonoBehaviour
{

    public static Settings instance;
    public Action<float> ChangeMouseSensitivity;
    public Action<float> ChangeVolume;
    //public Action<bool> ControllJostick;
    //[SerializeField] private Toggle _joystickToggle;
    [SerializeField] private GameObject _UIWindow;
    [SerializeField] private GameObject _exitButton;

    public bool IsReady;
    public Action Ready;

    public float Sensivity;

    //private bool _isOpen;


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
        //GameManager.Instance.LevelInProgress += OnLevelSwitch;
    }

    private void OnDisable()
    {
        //GameManager.Instance.LevelInProgress -= OnLevelSwitch;
    }

    private void Start()
    {
        
        MirraSDK.WaitForProviders(static () => {
            Settings.instance.OnGameStart();
            // Методы SDK не должны вызывать вылет или NullReferenceException,
            // делегат будет вызван только когда все провайдеры имеют статус IsInitialized.
        });
    }

    private void OnGameStart()
    {
        Sensitivity(SaveManager.Instance.LoadSensivity());
        IsReady = true;
        Ready?.Invoke();
    }

    private void OnLevelSwitch(bool inProgress)
    {
        if (_exitButton != null)
            _exitButton.gameObject.SetActive(inProgress);
    }


    // �������� ������ � ����� - ������ ��������
    private void Awake()
    {

        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

   
    public void Sensitivity(float sens)
    {
        Sensivity = sens;
        ChangeMouseSensitivity?.Invoke(sens);
        SaveManager.Instance.SaveSensivity(sens);
    }

    public void SoundVolume(float volume)
    {
        SoundManager.Instance.SoundVolume = volume;
    }
    public void MusicVolume(float volume)
    {
        SoundManager.Instance.MusicVolume = volume;
    }

/*    private void ToggleUIOpen()
    {
        _isOpen = !_isOpen;
        _UIWindow.SetActive(_isOpen);
        if (ControlManager.Instance && ControlManager.Instance.UseCursor != _isOpen)
        {
            ControlManager.Instance.UnlockMouse();
        }
    }*/

}

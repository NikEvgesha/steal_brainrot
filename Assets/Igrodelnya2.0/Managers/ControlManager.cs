using System.Collections;
using UnityEngine;

public class ControlManager : MonoBehaviour
{
    [SerializeField] private bool _useTouchControls;
    [SerializeField] private DeviceProvider _provider;
    [SerializeField] private GameObject _mobileControlsPrefab;
    private bool _cursorActive;
    private bool _moveActive = true;
    private bool _inspectorTouchOverride;
    public bool UseTouchControl { get { return _useTouchControls; } private set { } }

    private int _activeWindows = 0;
    public bool CursorActive
    {
        get
        {
            return _cursorActive;
        }

        set
        {
            if (_useTouchControls)
                return;

            if (value)
            {
                _activeWindows++;
            }
            else
            {
                _activeWindows = _activeWindows > 0 ? _activeWindows - 1 : 0 ;
                if (_activeWindows > 0) return;
            }

            ApplyCursorState(value);

            /*if (_moveActive)
                InventoryUI.Instance.ToggleOpen(false);*/

        }
    }
    public bool MoveActive
    {
        get
        {
            return _moveActive;
        }

        set
        {
            _moveActive = value;
        }
    }
    private void Awake()
    {
        if (G.Control == null)
        {
            G.Control = this;
            _inspectorTouchOverride = _useTouchControls;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        RefreshControlMode();
        if (_provider != null && !_provider.IsInitialized())
            StartCoroutine(WaitForDeviceProvider());
        //if (!_useTouchControls)
        //{
        //    CursorActive = false;  
        //}
        if (!_useTouchControls)
            ApplyCursorState(_cursorActive);
    }

    private IEnumerator WaitForDeviceProvider()
    {
        var wait = new WaitForSecondsRealtime(0.25f);
        while (_provider != null && !_provider.IsInitialized())
            yield return wait;

        RefreshControlMode();
    }

    private void RefreshControlMode()
    {
        bool providerReportsMobile = _provider != null &&
                                     _provider.IsInitialized() &&
                                     _provider.IsMobileDevice();
        bool useTouch = _inspectorTouchOverride || Application.isMobilePlatform || providerReportsMobile;
        _useTouchControls = useTouch;

        if (useTouch)
            EnsureMobileControls();

        if (ControlUI.Instance != null)
            ControlUI.Instance.UseMobileSetup(useTouch);
    }

    private void EnsureMobileControls()
    {
        if (ControlUI.Instance != null || _mobileControlsPrefab == null)
            return;

        GameObject controls = Instantiate(_mobileControlsPrefab);
        controls.name = _mobileControlsPrefab.name;
        DontDestroyOnLoad(controls);
    }

    private void ApplyCursorState(bool cursorActive)
    {
        _cursorActive = cursorActive;

        if (_provider && _provider.IsInitialized())
        {
            _provider.SetCursorLockState(CursorLockMode.None);
            _provider.SetCursorVisible(true);
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }



}

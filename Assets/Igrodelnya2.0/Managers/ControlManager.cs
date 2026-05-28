using UnityEngine;

public class ControlManager : MonoBehaviour
{
    [SerializeField] private bool _useTouchControls;
    [SerializeField] private DeviceProvider _provider;
    private bool _cursorActive;
    private bool _moveActive = true;
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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (_provider && _provider.IsInitialized())
        {
            if (_provider.IsMobileDevice())
            {
                _useTouchControls = true;
            }
        }
        else
        {
            if (Application.isMobilePlatform)
            {
                _useTouchControls = true;
            }
        }
        //if (!_useTouchControls)
        //{
        //    CursorActive = false;  
        //}
        if (!_useTouchControls)
            ApplyCursorState(_cursorActive);
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

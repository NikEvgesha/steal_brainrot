using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct TouchControls
{
    public OnScreenButton jumpButton;
    public OnScreenButton sprintButton;
    public OnScreenButton pickUpButton;
    public OnScreenButton putToInventoryButton;
    public OnScreenButton attachButton;
    public OnScreenButton reloadButton;
    public OnScreenButton attackButton;
    public OnScreenButton useButton;
    public OnScreenButton rotateXButton;
    public OnScreenButton rotateYButton;


    public GameObject textHoldButton;
    public OnScreenJoystick moveJoystick;
    public CameraTouchController cameraTouchController;
}

[Serializable]
public struct DesktopHints
{
    public GameObject common;
    public GameObject pickUp;
    public GameObject putToInventory;
    public GameObject attach;
    public GameObject rotate;
    public GameObject healing;
}
public class ControlUI : MonoBehaviour
{
    private static ControlUI _instance;
    public static ControlUI Instance => _instance;
    [SerializeField]
    private GameObject _mobileUI;
    [SerializeField]
    private GameObject _desktopUI;
    [SerializeField]
    private GameObject _desktopMenuUI;
    [SerializeField]
    private List<GameObject> _hotKeys; 

    [SerializeField] private TouchControls _touchControls;
    [SerializeField] private DesktopHints _descktopHints;
    private bool _isQuitting;
    private bool _isMobile;
    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }
    public void UseMobileSetup(bool isMobile)
    {
        _isMobile = isMobile;
        _mobileUI.SetActive(isMobile);
        _desktopUI.SetActive(!isMobile);
        _desktopMenuUI.SetActive(!isMobile);
        foreach (GameObject go in _hotKeys)
        {
            go.SetActive(!isMobile);
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        DeactivateAll();
    }
    private void DeactivateAll()
    {
        _descktopHints.rotate.SetActive(false);
        //_touchControls.rotateXButton.gameObject.SetActive(false);
    }
    /*    public void SwitchPlatformControls(bool onPlatform)
        {
            _touchControls.jumpButton.gameObject.SetActive(onPlatform);
        }*/

    public TouchControls GetTouchControls()
    {
        return _touchControls;
    }

    public void HideItemHints()
    {
        if (_isQuitting)
            return;
        if (!_isMobile)
        {
            _descktopHints.putToInventory.SetActive(false);
            _descktopHints.attach.SetActive(false);
            _descktopHints.rotate.SetActive(false);
        } else
        {
            _touchControls.rotateXButton.gameObject.SetActive(false);
            _touchControls.rotateYButton.gameObject.SetActive(false);
        }
    }

    /*public void ShowPickUpButton(bool visible)
    {
        if (_isQuitting)
            return;
        if (_isMobile)
        {
            _touchControls.pickUpButton.gameObject.SetActive(visible);

            if (Input.GetMouseButtonDown(0) && visible)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    PickableItem clickable = hit.collider.GetComponent<PickableItem>();
                    if (clickable != null)
                    {
                        PlayerInput.Instance.ForcePickUp = true;
                    }
                }
            }
        }
        else
            _descktopHints.pickUp.SetActive(visible);
    }
*/
    public void ShowPutToInventoryButton(bool visible)
    {
        if (_isQuitting)
            return;
        if (_isMobile)
            _touchControls.putToInventoryButton.gameObject.SetActive(visible);
        else
            _descktopHints.putToInventory.SetActive(visible);
    }

    public void OnItemPickUp(bool picked)
    {
        if (_isQuitting)
            return;
        _touchControls.putToInventoryButton.gameObject.SetActive(!picked);
    }

    public void ShowAttachButton(bool visible)
    {
        if (_isQuitting)
            return;
        if (_isMobile)
            _touchControls.attachButton.gameObject.SetActive(visible);
        else
            _descktopHints.attach.SetActive(visible);
    }
    
    public void ShowAttackButton(bool visible)
    {
        if (_isQuitting)
            return;
        if (_isMobile)
        {
            _touchControls.useButton.gameObject.SetActive(!visible);
            _touchControls.attackButton.gameObject.SetActive(visible);
        }
        else
        {
            _descktopHints.healing.SetActive(!visible);
        }
        //else
        //_descktopHints.attack.SetActive(visible);
    }

    public void ShowReloadButton(bool visible)
    {
        if (_isQuitting)
            return;
        if (_isMobile)
            _touchControls.reloadButton.gameObject.SetActive(visible);
        //else
            //_descktopHints.reload.SetActive(visible);
    }

    public void ShowUseButton(bool visible,bool hold = false)
    {
        if (_isQuitting)
            return;
        if (_isMobile)
        {
            _touchControls.useButton.gameObject.SetActive(visible);
            _touchControls.textHoldButton.SetActive(hold);
            _touchControls.attackButton.gameObject.SetActive(!visible);
        } 
        else
        {
            _descktopHints.healing.SetActive(hold);
        }
            
        //else
        //_descktopHints.reload.SetActive(visible);
    }

    public void ShowRotateButtons(bool visible)
    {
        if (_isQuitting)
            return;
        if (_isMobile)
        {
            _touchControls.rotateXButton.gameObject.SetActive(visible);
            _touchControls.rotateYButton.gameObject.SetActive(visible);
        } else
        {
            _descktopHints.rotate.gameObject.SetActive(visible);
        }
    }
}

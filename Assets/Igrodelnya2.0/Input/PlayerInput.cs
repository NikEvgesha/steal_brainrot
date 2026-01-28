using System;
using TMPro;
using TouchControlsKit;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInput : MonoBehaviour
{
    public Vector3 Movement { get; private set; }
    public Vector2 Rotation { get; private set; }

    public float TrainMove { get; private set; }

    private TouchControls _touchControls;

    public bool Sprint { get {
            return _sprint;
        } private set { } }

    public bool JumpTriggered
    {
        get
        {
            return _jump;
            /*var tmp = _jump;
            _jump = false;
            return tmp;*/
        }
        private set { }
    }

    public bool PickUp 
    { 
        get 
        {
            var tmp = _pickUp;
            _pickUp = false;
            return tmp;
        }
        private set { } 
    }
    public bool ForcePickUp
    {
        get
        {
            var tmp = _forcePickUp;
            _forcePickUp = false;
            return tmp;
        }
        set { _forcePickUp = value; }
    }
    public bool Interaction
    {
        get
        {
           return _interaction;
            //_interaction = false;
            //return tmp;
        }
        private set { }
    }

    public bool InteractionHold
    {
        get
        {
            return _interactionHold;
            //_interaction = false;
            //return tmp;
        }
        private set { }
    }

    public bool UseItem
    {
        get
        {
            return _attack || _healing;
        }
        private set { }
    }
    public bool Reload
    {
        get
        {
            return _reload;
        }
        private set { }
    }

    public bool RotationY
    {
        get
        {
            return _rotationY;
        }
        private set { }
    }

    public bool RotationX {
        get
        {
            return _rotationX;
        }
        private set { }
    }

    public bool Attach => _attach;
    public bool Inventory => _inventory;

    public bool Pause => _pause;


    private bool _jump;
    private bool _sprint;
    private bool _interaction;
    private bool _interactionHold;
    private bool _pickUp;
    private bool _forcePickUp;
    private bool _inTrain;
    private bool _attach;
    private bool _inventory;
    private bool _useItem;
    private bool _reload;
    private bool _attack;
    private bool _healing;
    private bool _rotationX;
    private bool _rotationY;
    private bool _pause;
    private bool _roulette;
    private bool _friends;
    private bool _playtime;


    public Action AJump;
    public Action ASprint;
    public Action AInteraction;
    public Action AInteractionHold;
    public Action APickUp;
    public Action AForcePickUp;
    public Action AInTrain;
    public Action AAttach;
    public Action AInventory;
    public Action AUseItem;
    public Action AReload;
    public Action AAttack;
    public Action AHealing;
    public Action APause;
    public Action ARoulette;
    public Action AFriends;
    public Action APlaytime;
    public Action<MonoBehaviour> AOpenWindow;

    private void Awake()
    {
        if (G.Input == null)
        {
            G.Input = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        //_touchControls = FindAnyObjectByType<ControlUI>().GetTouchControls();
    }

    private void Update()
    {
        if (TMP_InputFieldIsFocused())
            return;
        CheckControls();
        UpdateMovement();
        UpdateRotation();
    }
    bool TMP_InputFieldIsFocused()
    {
        var go = EventSystem.current?.currentSelectedGameObject;
        if (go == null) return false;

        return go.GetComponent<TMP_InputField>() != null;
    }
    private void CheckControls()
    {
       /* if (Input.GetKeyDown(KeyCode.Tab))
        {
            //ShowCursor(!IsCursorVisible);
            G.Control.CursorActive = !G.Control.CursorActive;
        }*/

        if (G.Control.UseTouchControl)
        {
            //_jump = _touchControls.jumpButton.IsTriggered;
            //_pickUp = _touchControls.pickUpButton.IsTriggered;
            //_interaction = _touchControls.putToInventoryButton.IsTriggered;
            //_interactionHold = _touchControls.putToInventoryButton.IsHolded;
            //_sprint = _touchControls.sprintButton.IsHolded;
            //_attach = _touchControls.attachButton.IsTriggered;
            //_attack = _touchControls.attackButton.IsHolded;
            //_healing = _touchControls.useButton.IsHolded;
            //_reload = _touchControls.reloadButton.IsTriggered;
            //_rotationX = _touchControls.rotateXButton.IsHolded;
            //_rotationY = _touchControls.rotateYButton.IsHolded;
        }
        else
        {
            _jump = Input.GetKeyDown(KeyCode.Space);
            _interaction = Input.GetKeyDown(KeyCode.E);
            _interactionHold = Input.GetKey(KeyCode.E);
            _sprint = Input.GetKey(KeyCode.LeftShift);
            //_attach = Input.GetKeyDown(KeyCode.Z);
            //_inventory = Input.GetKeyDown(KeyCode.Tab);
            //_reload = Input.GetKeyDown(KeyCode.R);
            //_rotationY = _interactionHold;
            //_rotationX = Input.GetKey(KeyCode.Q);
            _pause = Input.GetKeyDown(KeyCode.P);
            _roulette = Input.GetKeyDown(KeyCode.K);
            _friends = Input.GetKeyDown(KeyCode.U);
            _playtime = Input.GetKeyDown(KeyCode.L);
            //if (!G.Control.CursorActive)
            //{
            //    _pickUp = Input.GetMouseButtonDown(1);
            //    //_useItem = Input.GetMouseButtonDown(0);
            //    _attack = Input.GetMouseButton(0);
            //    _healing = _attack;
            //}
        }
        //if (_jump) AJump?.Invoke();
        if (_sprint) ASprint?.Invoke();
        if (_interaction) AInteraction?.Invoke();
        if (_interactionHold) AInteractionHold?.Invoke();
        //if (_pickUp) APickUp?.Invoke();
        //if (_forcePickUp) AForcePickUp?.Invoke();
        //if (_inTrain) AInTrain?.Invoke();
        //if (_attach) AAttach?.Invoke();
        //if (_inventory) AInventory?.Invoke();
        //if (_useItem) AUseItem?.Invoke();
        //if (_reload) AReload?.Invoke();
        //if (_attack) AAttack?.Invoke();
        //if (_healing) AHealing?.Invoke();
        if (_pause) APause?.Invoke();
        if (_roulette) ARoulette?.Invoke();
        if (_friends) AFriends?.Invoke();
        if (_playtime) APlaytime?.Invoke();

    // _useItem = _pickUp; //Переработать смысл кнопки

}

    private void UpdateMovement()
    {

        if (G.Control.UseTouchControl)
        {
            Movement = new Vector3(_touchControls.moveJoystick.Horizontal(), 0f, _touchControls.moveJoystick.Vertical());
        } else
        {
            Movement = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;
        }

        if (_inTrain)
        {
            TrainMove = Movement.z;
            Movement = new Vector3(0f, 0f, 0f);
            return;
        }
    }


    public void UpdateRotation()
    {
        if (G.Control.UseTouchControl)
        {
            //Rotation = _touchControls.cameraTouchController.GetRotationInput();
            Rotation = TCKInput.GetAxis("Touchpad");
        }
        else
        {
            Rotation = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }
    }

    public void SitTrain(bool inTrain)
    {
        _inTrain = inTrain;
    }
    public bool InTrain()
    {
        return _inTrain;
    }
    public void UseAttack(bool use)
    {

    }
}

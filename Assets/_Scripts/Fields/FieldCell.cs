using UnityEngine;
using UnityEngine.Events;

public class FieldCell : MonoBehaviour
{
    [SerializeField] private GameObject _triggerIndicator;
    [SerializeField] private GameObject _takeButton;
    [SerializeField] private GameObject _dropButton;
    [SerializeField] private GameObject _addSpeedButton;

    [HideInInspector] public UnityEvent PlayerEnter;
    [HideInInspector] public UnityEvent PlayerExit;
    [HideInInspector] public UnityEvent TakeBrainrot;
    [HideInInspector] public UnityEvent SpeedBoost;
    private Item _inField;

    private bool _playerOnCell;

    public void _OnPlayerEnter()
    {
        TestBackpackBrainrot.Instance.SwichItem.AddListener(CheckPlayer);
        TestBackpackBrainrot.Instance.PlaceItem.AddListener(UpdateFieldItem);
        CheckPlayer();
        _playerOnCell = true;
        PlayerEnter?.Invoke();
    }
    public void CheckPlayer()
    {
        _dropButton.SetActive(false);
        _addSpeedButton.SetActive(false);
        _triggerIndicator.SetActive(false);
        switch (_inField)
        {
            case Item.Free:
                FieldFree();
                break;
            case Item.Egg:
                _addSpeedButton.SetActive(true);
                break;
            case Item.Brainrot:
                if (TestBackpackBrainrot.Instance.CheckHand() == Item.Hamer)
                {
                    _takeButton.SetActive(true);
                }
                break;
            default:
                break;
        }
    }
    private void FieldFree()
    {
        switch (TestBackpackBrainrot.Instance.CheckHand())
        {
            case Item.Egg:
                _triggerIndicator.SetActive(true);
                _dropButton.SetActive(true);
                break;
            case Item.Brainrot:
                _triggerIndicator.SetActive(true);
                _dropButton.SetActive(true);
                break;
            default:
                break;
        }
    }
    public void UpdateFieldItem(Item item)
    {
        _inField = item;
    }
    public void _OnPlayerExit()
    {
        TestBackpackBrainrot.Instance.PlaceItem.RemoveListener(UpdateFieldItem);
        TestBackpackBrainrot.Instance.SwichItem.RemoveListener(CheckPlayer);
        _dropButton.SetActive(false);
        _addSpeedButton.SetActive(false);
        _takeButton.SetActive(false);
        _triggerIndicator.SetActive(false);
        _playerOnCell = false;
        PlayerExit?.Invoke();
    }
    public void _Take()
    {
        TakeBrainrot?.Invoke();
    }
    public void _Drop()
    {
        TestBackpackBrainrot.Instance.Drop(this);
    }
    public void _AddSpeed()
    {
        SpeedBoost?.Invoke();
    }
}

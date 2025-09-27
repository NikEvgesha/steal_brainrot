using UnityEngine;
using UnityEngine.Events;
using static UnityEditor.Progress;

public class TestBackpackBrainrot : MonoBehaviour
{
    public static TestBackpackBrainrot Instance;
    public Brainrot BrainrotObj;

    private BrainrotData _data; 
    private EggData _eggData;
    private BrainrotDinamicData _brainrotDinamicData;
    private FieldCell _floorListener;
    private Brainrot _currentBrainrot;
    private Egg _currentEgg;
    private bool _use = false;
    private Item _inHand;

    [HideInInspector] public UnityEvent SwichItem = new UnityEvent();
    [HideInInspector] public UnityEvent<Item> PlaceItem = new UnityEvent<Item>();

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public void _GetBrainrot()
    {
        if (!_use) return; _use = false;
        Destroy(_currentBrainrot.gameObject);
        _currentBrainrot = null;

    }
    public Item CheckHand()
    {
        return _inHand;
    }
    public void TakeEgg(Egg egg)
    {
        _currentEgg= egg;
        _inHand = Item.Egg;
        PlayerManager.Instance.SetItem(egg.transform);
        //egg.transform.SetParent(PlayerManager.Instance.transform);
        //egg.transform.localPosition = Vector3.up;
    }
    public void TakeBrainrot(Brainrot brainrot)
    {
        _currentBrainrot = brainrot;
        _inHand = Item.Brainrot;
        PlayerManager.Instance.SetItem(brainrot.transform);
        //brainrot.transform.SetParent(PlayerManager.Instance.transform);
        //brainrot.transform.localPosition = Vector3.up;
    }
    public void Drop(FieldCell field)
    {
        PlaceItem?.Invoke(_inHand);
        _floorListener = field;
        switch (_inHand)
        {
            case Item.Egg:
                PlayerManager.Instance.RemoveItem();
                _currentEgg.transform.SetParent(_floorListener.transform);
                _currentEgg.transform.localPosition = Vector3.zero;
                _currentEgg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _currentEgg.InitTimer(_floorListener);
                _currentEgg = null;
                _inHand = Item.Hamer;
                SwichItem?.Invoke();
                break;
            case Item.Brainrot:
                PlayerManager.Instance.RemoveItem();
                _currentBrainrot.transform.SetParent(_floorListener.transform);
                _currentBrainrot.transform.localPosition = Vector3.zero;
                _currentBrainrot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _currentBrainrot.NewPlace(_floorListener);
                _currentBrainrot = null;
                _inHand = Item.Hamer;
                SwichItem?.Invoke();

                break;
            default:
                break;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class QuickAccessManager : MonoBehaviour
{
    private static QuickAccessManager _instance;
    public static QuickAccessManager Instance { get { return _instance; } }


    [SerializeField] private int _capacity = 8;
    [SerializeField] private List<InventoryItem> _startItems;

    private List<InventoryItem> _items;
    private InventoryItem _currentActive;
    private FieldCell _floorListener;
    private Item _inHand;
    private bool _dropping;

    public List<InventoryItem> Items => _items;
    public InventoryItem CurrentActive => _currentActive;


    [HideInInspector] public UnityEvent<List<InventoryItem>> ItemsUpdated;
    [HideInInspector] public UnityEvent<InventoryItem> SwitchActiveItem = new UnityEvent<InventoryItem>();
    [HideInInspector] public UnityEvent<Item> PlaceItem = new UnityEvent<Item>();

    public int Capacity { get { return _capacity; } }


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
        _items = new List<InventoryItem>(_capacity);
    }

    private void Start()
    {
        _inHand = Item.Free;
        foreach (InventoryItem it in _startItems)
        {
            InventoryItem item = Instantiate(it, PlayerManager.Instance.transform);
            Add(item);
        }
        SwitchActive(null);
    }


    public bool Add(InventoryItem item)
    {
        if (_items.Count == _capacity)
        {
            return false;
        }

        _items.Add(item);
        item.InQuickAccess = true;
        ItemsUpdated.Invoke(_items);
        SwitchActive(item);
        return true;
    }


    public void Remove(InventoryItem item)
    {
        _items.Remove(item);
        item.InQuickAccess = false;
        if (_currentActive == item)
        {
            SwitchActive(null);           
        }
        ItemsUpdated.Invoke(_items);
    }

    public void DropCurrent(FieldCell field)
    {
        _dropping = true;
        PlaceItem?.Invoke(_currentActive.Type);
        _floorListener = field;
        _currentActive.transform.SetParent(_floorListener.transform);
        _currentActive.transform.localPosition = Vector3.zero;
        _currentActive.transform.localRotation = Quaternion.identity;
        _inHand = Item.Hamer; // get next item
        switch (_currentActive.Type)
        {
            case Item.Egg:
                _currentActive.GetComponent<Egg>().InitTimer(_floorListener);
                break;
            case Item.Brainrot:
                _currentActive.GetComponent<Brainrot>().NewPlace(_floorListener);
                break;
            default:
                break;
        }
        Inventory.Instance.Remove(_currentActive);
        _dropping = false;
    }


    public Item CheckHand()
    {
        return _inHand;
    }


    public void SwitchActive(InventoryItem item)
    {
        if (_currentActive != null && !_dropping)
        {
            _currentActive.gameObject.SetActive(false);
        }
       
        _currentActive = item;
        
        if (_currentActive != null)
        {
            _currentActive.gameObject.SetActive(true);
            _inHand = _currentActive.Type;
            PlayerManager.Instance.SetItem(item);
        } else
        {
            _inHand = Item.Free;
            PlayerManager.Instance.RemoveItem();
        }

        SwitchActiveItem?.Invoke(_currentActive);
    }


    public void OnUIInitialized()
    {
        ItemsUpdated.Invoke(_items);
    }
}

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
        foreach (InventoryItem item in _startItems)
        {
            if (_items.Count < _capacity)
                _items.Add(item);
        }
        ItemsUpdated.Invoke(_items);
    }


    public bool Add(InventoryItem item)
    {
        if (_items.Count == _capacity)
        {
            return false;
        }

        if (_currentActive != null)
        {
            _currentActive.gameObject.SetActive(false);
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
            _currentActive = null;
            SwitchActive(null);
        }
        ItemsUpdated.Invoke(_items);
    }

    public void DropCurrent(FieldCell field)
    {
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
    }


    public Item CheckHand()
    {
        return _inHand;// _currentActive != null ? _currentActive.Type : Item.Free;
    }


    public void SwitchActive(InventoryItem item)
    {
        if (_currentActive != null)
        {
            _currentActive.gameObject.SetActive(false);
        }


        _currentActive = item;
        SwitchActiveItem?.Invoke(_currentActive);

        if (_currentActive != null)
        {
            _currentActive.gameObject.SetActive(true);
            _inHand = _currentActive.Type;
        }

       
    }
}

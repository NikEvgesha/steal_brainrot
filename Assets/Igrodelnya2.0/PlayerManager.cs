using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private static PlayerManager _instance;
    public static PlayerManager Instance { get { return _instance; } private set { } }

    [SerializeField] private Transform _getPoint;
    [SerializeField] private Transform _handPoint;

    private TPPlayerController _tPPlayer;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            _tPPlayer = GetComponent<TPPlayerController>();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void SetItem(InventoryItem item)
    {
        if (item.Type == Item.Hamer)
        {
            item.transform.SetParent(_handPoint.transform);
        } else
        {
            item.transform.SetParent(_getPoint.transform);
        }
            
        item.transform.localPosition = Vector3.zero;
        //item.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        _tPPlayer.SetHolding(true);
    }
    public void RemoveItem()
    {
        _tPPlayer.SetHolding(false);
    }
    
}

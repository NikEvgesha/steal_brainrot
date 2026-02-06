using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private Transform _getPoint;
    [SerializeField] private Transform _handPoint;
    [SerializeField] private TPCameraController _camera;
    [SerializeField] private Transform _cameraPivot;

    private TPPlayerController _tPPlayer;

    private void Awake()
    {
        if (G.Player == null)
        {
            G.Player = this;
            _tPPlayer = GetComponent<TPPlayerController>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Init(Transform spawnPos)
    {
        //transform.position = spawnPos.position;
        TPCameraController camera = Instantiate(_camera);
        camera.SetTarget(_cameraPivot);
        _tPPlayer.SetCamera(camera.transform);
    }

    public void SetItem(InventoryItem item)
    {
        if (item.Type == Item.Hamer)
        {
            item.transform.SetParent(_handPoint.transform);
        } else
        {
            item.transform.SetParent(_getPoint.transform);
            _tPPlayer.SetHolding(true);
        }
            
        item.transform.localPosition = Vector3.zero;
        //item.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
       
    }
    public void RemoveItem()
    {
        _tPPlayer.SetHolding(false);
    }
    
}

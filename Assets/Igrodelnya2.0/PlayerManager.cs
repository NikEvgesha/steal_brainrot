using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private Transform _getPoint;
    [SerializeField] private Transform _handPoint;
    [SerializeField] private TPCameraController _camera;
    [SerializeField] private Transform _cameraPivot;

    private TPPlayerController _tPPlayer;
    private CharacterController _controller;
    private InventoryItem _heldItem;

    private void Awake()
    {
        if (G.Player == null)
        {
            G.Player = this;
            _tPPlayer = GetComponent<TPPlayerController>();
            _controller = GetComponent<CharacterController>();
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
        _camera = camera;
        _camera.SetTarget(_cameraPivot);
        _tPPlayer.SetCamera(camera.transform);
    }

    public void SetItem(InventoryItem item)
    {
        if (_heldItem != null && _heldItem != item)
            SetItemCollidersEnabled(_heldItem, true);

        if (item.Type == Item.Hamer)
        {
            item.transform.SetParent(_handPoint.transform);
            _tPPlayer.SetHolding(false);
        }
        else
        {
            item.transform.SetParent(_getPoint.transform);
            _tPPlayer.SetHolding(true);
        }
            
        item.transform.localPosition = Vector3.zero;
        SetItemCollidersEnabled(item, false);
        _heldItem = item;
        //item.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
       
    }
    public void RemoveItem()
    {
        if (_heldItem != null)
        {
            SetItemCollidersEnabled(_heldItem, true);
            _heldItem = null;
        }

        _tPPlayer.SetHolding(false);
    }
    
    public void Teleport(Transform position)
    {
        //_tPPlayer.SetTeleportPosition(position);
        _controller.enabled = false;
        transform.position = position.position;
        transform.rotation = position.rotation;
        _controller.enabled = true;
        _camera.ResetCamera();
        
    }

    private static void SetItemCollidersEnabled(InventoryItem item, bool isEnabled)
    {
        if (item == null)
            return;

        var colliders = item.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = isEnabled;
        }
    }
}

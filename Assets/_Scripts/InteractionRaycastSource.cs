using UnityEngine;

public class InteractionRaycastSource : MonoBehaviour
{

    [SerializeField] private float _raycastDistance;
    [SerializeField] private LayerMask _layer;

    private InteractionRaycastListener _lastHit;
    private void FixedUpdate()
    {
        TryHit(Vector3.down);
    }
    private bool TryHit(Vector3 direction)
    {
        Ray ray = new Ray(transform.position, direction);
        if (Physics.Raycast(ray, out RaycastHit hit, _raycastDistance, _layer) &&
            hit.transform.TryGetComponent(out InteractionRaycastListener listener))
        {
            if (_lastHit != listener)
            {
                if (_lastHit != null)
                    _lastHit.onRaycastFail();
                _lastHit = listener;
                _lastHit.onRaycastHit();

            }
            return true;
        }
        else
        {
            if (_lastHit != null)
            {
                _lastHit.onRaycastFail();
                _lastHit = null;
            }
            return false;
        }
    } 
}

using UnityEngine;

public class InteractionRaycastSource : MonoBehaviour
{
    [SerializeField] private float _raycastDistance;
    [SerializeField] private LayerMask _layer;

    private InteractionRaycastListener _lastHit;
    private void FixedUpdate()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
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
            
        } else
        {
            if (_lastHit != null)
            {
                _lastHit.onRaycastFail();
                _lastHit = null;
            }
        }
    }
}

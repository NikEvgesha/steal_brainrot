using UnityEngine;

public class InteractionRaycastSource : MonoBehaviour
{
    [SerializeField] private RaycastType _raycastType;
    [SerializeField] private float _raycastDistance = 3f;

    [Header("Layers")]
    [SerializeField] private LayerMask _interactableMask; // объекты с InteractionRaycastListener
    [SerializeField] private LayerMask _occluderMask;     // стены/пол/преп€тстви€

    private InteractionRaycastListener _lastHit;
    private Vector3 _direction;

    private void FixedUpdate()
    {
        TryHit();
    }

    private bool TryHit()
    {
        _direction = _raycastType switch
        {
            RaycastType.Down => Vector3.down,
            RaycastType.Forward => transform.forward,
            _ => Vector3.zero
        };
        if (_direction == Vector3.zero) return false;

        var origin = transform.position;

        float maxVisibleDist = _raycastDistance;
        if (Physics.Raycast(origin, _direction, out var occHit, _raycastDistance, _occluderMask, QueryTriggerInteraction.Ignore))
        {
            maxVisibleDist = occHit.distance; // дальше Ц стена
            //Debug.Log($"Occluder: {occHit.transform.name}, dist: {occHit.distance:0.###}");
        }
        if (Physics.Raycast(origin, _direction, out var intHit, _raycastDistance, _interactableMask, QueryTriggerInteraction.Collide)
            && intHit.distance <= maxVisibleDist)
        {
            var listener = intHit.transform.GetComponent<InteractionRaycastListener>();
            if (listener == null)
                listener = intHit.transform.GetComponentInParent<InteractionRaycastListener>();

            if (listener == null)
            {
                if (_lastHit != null)
                {
                    _lastHit.onRaycastFail();
                    _lastHit = null;
                }
                return false;
            }

            if (_raycastType != RaycastType.Down &&
                listener.MaxDistance <= Vector3.Distance(transform.position, listener.transform.position))
            {
                if (_lastHit != null)
                {
                    _lastHit.onRaycastFail();
                    _lastHit = null;
                }
                return false;
            }

            //Debug.Log(intHit.transform.gameObject.name+": " + intHit.distance);
            if (_lastHit != listener)
            {
                _lastHit?.onRaycastFail();
                _lastHit = listener;
                _lastHit.onRaycastHit();
            }
            return true;
        }

        if (_lastHit != null)
        {
            _lastHit.onRaycastFail();
            _lastHit = null;
        }
        return false;
    }

}

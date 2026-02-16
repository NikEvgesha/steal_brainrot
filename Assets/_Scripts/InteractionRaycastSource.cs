using UnityEngine;

public class InteractionRaycastSource : MonoBehaviour
{
    [SerializeField] private RaycastType _raycastType;
    [SerializeField] private float _raycastDistance = 3f;
    [SerializeField] private bool _useSecondaryRaycast;
    [SerializeField] private float _secondaryRaycastDistance = 3f;

    [Header("Layers")]
    [SerializeField] private LayerMask _interactableMask; // objects with InteractionRaycastListener
    [SerializeField] private LayerMask _occluderMask;     // walls/floor/obstacles
    [SerializeField] private float _forwardRayOriginHeight = 1.3f;

    private InteractionRaycastListener _lastHit;

    private void FixedUpdate()
    {
        TryHit();
    }

    private bool TryHit()
    {
        var hasPrimary = TryHitByType(_raycastType, _raycastDistance, out var primary);
        var hasSecondary = false;
        InteractionRaycastListener secondary = null;
        if (_useSecondaryRaycast)
        {
            var secondaryType = _raycastType == RaycastType.Down ? RaycastType.Forward : RaycastType.Down;
            hasSecondary = TryHitByType(secondaryType, _secondaryRaycastDistance, out secondary);
        }

        if (hasPrimary)
        {
            if (hasSecondary && IsRemoteInteractionTarget(secondary))
            {
                SetCurrentHit(secondary);
                return true;
            }

            SetCurrentHit(primary);
            return true;
        }

        if (hasSecondary)
        {
            SetCurrentHit(secondary);
            return true;
        }

        if (_lastHit != null)
        {
            _lastHit.onRaycastFail();
            _lastHit = null;
        }

        return false;
    }

    private static bool IsRemoteInteractionTarget(InteractionRaycastListener listener)
    {
        if (listener == null)
            return false;
        return listener.GetComponentInParent<RemoteFriendBoard>() != null;
    }

    private bool TryHitByType(RaycastType raycastType, float raycastDistance, out InteractionRaycastListener listener)
    {
        listener = null;

        var direction = raycastType switch
        {
            RaycastType.Down => Vector3.down,
            RaycastType.Forward => transform.forward,
            _ => Vector3.zero
        };

        if (direction == Vector3.zero || raycastDistance <= 0f)
            return false;

        var origin = transform.position;
        if (raycastType == RaycastType.Forward && _forwardRayOriginHeight > 0f)
            origin += Vector3.up * _forwardRayOriginHeight;
        var maxVisibleDistance = raycastDistance;

        if (Physics.Raycast(origin, direction, out var occHit, raycastDistance, _occluderMask, QueryTriggerInteraction.Ignore))
            maxVisibleDistance = occHit.distance;

        if (!Physics.Raycast(origin, direction, out var intHit, raycastDistance, _interactableMask, QueryTriggerInteraction.Collide))
            return false;

        if (intHit.distance > maxVisibleDistance)
            return false;

        listener = intHit.transform.GetComponent<InteractionRaycastListener>();
        if (listener == null)
            listener = intHit.transform.GetComponentInParent<InteractionRaycastListener>();

        if (listener == null)
            return false;

        if (raycastType != RaycastType.Down &&
            listener.MaxDistance <= Vector3.Distance(transform.position, listener.transform.position))
            return false;

        return true;
    }

    private void SetCurrentHit(InteractionRaycastListener listener)
    {
        if (listener == null)
            return;

        if (_lastHit == listener)
            return;

        _lastHit?.onRaycastFail();
        _lastHit = listener;
        _lastHit.onRaycastHit();
    }
}

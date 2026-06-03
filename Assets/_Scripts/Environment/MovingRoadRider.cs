using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class MovingRoadRider : MonoBehaviour
{
    [SerializeField] private float probeDistanceBelowFeet = 0.35f;
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField] private float obstacleProbeExtraDistance = 0.08f;
    [SerializeField] private float obstacleNormalDotThreshold = 0.35f;

    private readonly RaycastHit[] _hits = new RaycastHit[8];
    private readonly Collider[] _overlaps = new Collider[8];
    private CharacterController _controller;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        if (_controller == null || !_controller.enabled)
            return;

        if (!TryFindMovingRoad(out MovingRoad movingRoad))
            return;

        if (!movingRoad.CanAffect(_controller))
            return;

        Vector3 velocity = movingRoad.GetWorldVelocity();
        if (velocity.sqrMagnitude < 0.0001f)
            return;

        Vector3 displacement = velocity * Time.deltaTime;
        if (IsBlockedByObstacle(displacement))
            return;

        _controller.Move(displacement);
    }

    private bool TryFindMovingRoad(out MovingRoad movingRoad)
    {
        movingRoad = null;

        Vector3 up = transform.up;
        Vector3 origin = transform.TransformPoint(_controller.center);
        float castDistance = Mathf.Max(_controller.height * 0.5f + probeDistanceBelowFeet, probeDistanceBelowFeet);
        int hitCount = Physics.RaycastNonAlloc(origin, -up, _hits, castDistance, surfaceMask, QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
            return false;

        float bestDistance = float.MaxValue;
        MovingRoad bestRoad = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _hits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                continue;

            MovingRoad road = hitCollider.GetComponentInParent<MovingRoad>();
            if (road == null || _hits[i].distance >= bestDistance)
                continue;

            bestDistance = _hits[i].distance;
            bestRoad = road;
        }

        movingRoad = bestRoad;
        return movingRoad != null;
    }

    private bool IsBlockedByObstacle(Vector3 displacement)
    {
        Vector3 direction = Vector3.ProjectOnPlane(displacement, transform.up);
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
            return false;

        direction /= distance;
        GetControllerCapsule(out Vector3 bottom, out Vector3 top, out float radius);

        if (HasBlockingOverlap(direction, bottom, top, radius))
            return true;

        float castDistance = distance + Mathf.Max(0f, obstacleProbeExtraDistance);
        int hitCount = Physics.CapsuleCastNonAlloc(
            bottom,
            top,
            radius,
            direction,
            _hits,
            castDistance,
            surfaceMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _hits[i].collider;
            if (!IsBlockingCollider(hitCollider))
                continue;

            if (IsGroundNormal(_hits[i].normal))
                continue;

            if (Vector3.Dot(_hits[i].normal, direction) <= -obstacleNormalDotThreshold)
                return true;
        }

        return false;
    }

    private bool HasBlockingOverlap(Vector3 direction, Vector3 bottom, Vector3 top, float radius)
    {
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            _overlaps,
            surfaceMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = _overlaps[i];
            if (!IsBlockingCollider(overlap))
                continue;

            if (!Physics.ComputePenetration(
                _controller,
                transform.position,
                transform.rotation,
                overlap,
                overlap.transform.position,
                overlap.transform.rotation,
                out Vector3 separationDirection,
                out _))
            {
                continue;
            }

            if (IsGroundNormal(separationDirection))
                continue;

            if (Vector3.Dot(separationDirection, direction) <= -obstacleNormalDotThreshold)
                return true;
        }

        return false;
    }

    private bool IsBlockingCollider(Collider target)
    {
        if (target == null || target.transform.IsChildOf(transform))
            return false;

        return target.GetComponentInParent<MovingRoad>() == null;
    }

    private bool IsGroundNormal(Vector3 normal)
    {
        return Vector3.Dot(normal, transform.up) > 0.65f;
    }

    private void GetControllerCapsule(out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 center = transform.TransformPoint(_controller.center);
        Vector3 up = transform.up;
        Vector3 scale = transform.lossyScale;
        float heightScale = Mathf.Abs(scale.y);
        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

        radius = Mathf.Max(0.01f, _controller.radius * radiusScale);
        float height = Mathf.Max(_controller.height * heightScale, radius * 2f);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

        bottom = center - up * halfSegment;
        top = center + up * halfSegment;
    }
}

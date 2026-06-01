using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class MovingRoadRider : MonoBehaviour
{
    [SerializeField] private float probeDistanceBelowFeet = 0.35f;
    [SerializeField] private LayerMask surfaceMask = ~0;

    private readonly RaycastHit[] _hits = new RaycastHit[8];
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

        _controller.Move(velocity * Time.deltaTime);
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
}

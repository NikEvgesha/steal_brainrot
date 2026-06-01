using UnityEngine;

[DisallowMultipleComponent]
public sealed class MovingRoad : MonoBehaviour
{
    [SerializeField] private Transform directionSource;
    [SerializeField] private Vector3 localDirection = Vector3.forward;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private bool flattenDirectionY = true;
    [SerializeField] private bool affectOnlyLocalPlayer = true;

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    public void Configure(Transform source, Vector3 direction, float speed, bool flattenY, bool localPlayerOnly)
    {
        directionSource = source;
        localDirection = direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward;
        moveSpeed = Mathf.Max(0f, speed);
        flattenDirectionY = flattenY;
        affectOnlyLocalPlayer = localPlayerOnly;
    }

    public bool CanAffect(CharacterController controller)
    {
        if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            return false;

        if (!affectOnlyLocalPlayer)
            return true;

        return G.Player != null && controller.GetComponentInParent<PlayerManager>() == G.Player;
    }

    public Vector3 GetWorldVelocity()
    {
        return GetWorldDirection() * moveSpeed;
    }

    private void Reset()
    {
        directionSource = transform;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        if (localDirection.sqrMagnitude < 0.0001f)
            localDirection = Vector3.forward;
    }

    private Vector3 GetWorldDirection()
    {
        Transform source = directionSource != null ? directionSource : transform;
        Vector3 direction = source.TransformDirection(localDirection);

        if (flattenDirectionY)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return direction.normalized;
    }
}

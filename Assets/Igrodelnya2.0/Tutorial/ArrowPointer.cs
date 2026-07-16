using UnityEngine;
using UnityEngine.AI;

public class ArrowPointer : MonoBehaviour
{
    public static ArrowPointer Instance { get; private set; }

    [Tooltip("World-space target for the arrow")]
    public Transform target;

    [Tooltip("Optional NavMeshAgent; its steering target takes precedence")]
    public NavMeshAgent agent;

    [Tooltip("Turn speed in degrees per second")]
    public float turnSpeed = 540f;

    [Tooltip("Hide the arrow within this distance")]
    public float hideDistance = 1.0f;

    [Tooltip("Reserved ground offset")]
    public float groundRayHeight = 0.2f;

    [Tooltip("Reserved ground mask")]
    public LayerMask groundMask = ~0;

    private Renderer[] _renderers;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        Vector3? targetPosition = GetTargetPosition();
        if (!targetPosition.HasValue)
        {
            SetVisible(false);
            return;
        }

        Vector3 position = targetPosition.Value;
        float distance = Vector3.Distance(position, transform.position);
        SetVisible(distance > hideDistance);

        // Rotate toward the target only on the horizontal plane.
        Vector3 direction = position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private Vector3? GetTargetPosition()
    {
        if (agent != null && agent.hasPath)
            return agent.steeringTarget;
        if (target != null)
            return target.position;
        return null;
    }

    private void SetVisible(bool visible)
    {
        if (_renderers == null)
            return;
        foreach (Renderer targetRenderer in _renderers)
            targetRenderer.enabled = visible;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
#endif
}

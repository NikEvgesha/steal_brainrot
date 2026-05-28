using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MovingRoad : MonoBehaviour
{
    [SerializeField] private Transform directionSource;
    [SerializeField] private Vector3 localDirection = Vector3.forward;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private bool flattenDirectionY = true;
    [SerializeField] private bool affectOnlyLocalPlayer = true;

    private readonly HashSet<CharacterController> _controllers = new HashSet<CharacterController>();
    private readonly List<CharacterController> _removeBuffer = new List<CharacterController>();

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

    private void Reset()
    {
        directionSource = transform;
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        if (localDirection.sqrMagnitude < 0.0001f)
            localDirection = Vector3.forward;
    }

    private void OnDisable()
    {
        _controllers.Clear();
        _removeBuffer.Clear();
    }

    private void LateUpdate()
    {
        if (_controllers.Count == 0 || moveSpeed <= 0f)
            return;

        Vector3 direction = GetWorldDirection();
        Vector3 offset = direction * (moveSpeed * Time.deltaTime);

        foreach (CharacterController controller in _controllers)
        {
            if (!IsValidController(controller))
            {
                _removeBuffer.Add(controller);
                continue;
            }

            controller.Move(offset);
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
            _controllers.Remove(_removeBuffer[i]);

        _removeBuffer.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAdd(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryAdd(other);
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterController controller = other.GetComponentInParent<CharacterController>();
        if (controller != null)
            _controllers.Remove(controller);
    }

    private void TryAdd(Collider other)
    {
        CharacterController controller = other.GetComponentInParent<CharacterController>();
        if (!IsValidController(controller))
            return;

        _controllers.Add(controller);
    }

    private bool IsValidController(CharacterController controller)
    {
        if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            return false;

        if (!affectOnlyLocalPlayer)
            return true;

        return G.Player != null && controller.GetComponentInParent<PlayerManager>() == G.Player;
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

using UnityEngine;

/// <summary>
/// Камера третьего лица с плавным орбитальным управлением, зумом и SphereCast-антиклиппингом.
/// Длинный кадр не может превратиться в резкий поворот: ввод и сглаживание используют
/// ограниченный unscaled timestep, а фактический угол догоняет целевой независимо от FPS.
/// </summary>
[DefaultExecutionOrder(50)]
public class TPCameraController : MonoBehaviour
{
    [Header("Target & Orbit")]
    [SerializeField] private Transform target;
    [SerializeField] private float yaw;
    [SerializeField] private float pitch = 15f;
    [SerializeField] private Vector2 pitchLimits = new Vector2(-40f, 70f);

    [Header("Sensitivity")]
    [SerializeField] private float yawSpeed = 180f;
    [SerializeField] private float pitchSpeed = 120f;
    [SerializeField] private bool rotateOnRightMouse = true;

    [Header("Zoom")]
    [SerializeField] private float minDistance = 1.6f;
    [SerializeField] private float maxDistance = 18f;
    [SerializeField] private float zoomSpeed = 3f;
    [SerializeField] private float backReturnSpeed = 8f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionPadding = 0.1f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private bool ignoreEggsInCollision = true;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.045f;
    [SerializeField, Min(90f)] private float maxAngularSpeed = 720f;
    [SerializeField, Min(0.001f)] private float maxInputDeltaTime = 0.0333f;
    [SerializeField, Min(0.1f)] private float maxLookInputPerFrame = 4f;
    [SerializeField] private bool freezeOnSpike = true;
    [SerializeField, Min(0.01f)] private float spikeThreshold = 0.05f;

    private float _desiredDistance;
    private float _currentDistance;
    private float _targetYaw;
    private float _targetPitch;
    private float _yawVelocity;
    private float _pitchVelocity;
    private bool _orbitInitialized;
    private RaycastHit _hit;
    private readonly RaycastHit[] _collisionHits = new RaycastHit[32];

    private void Awake()
    {
        InitializeOrbitAngles();
    }

    public void SetTarget(Transform value)
    {
        target = value;
        if (target == null)
            return;

        _desiredDistance = Mathf.Clamp(Vector3.Distance(transform.position, target.position), minDistance, maxDistance);
        _currentDistance = _desiredDistance;
        yaw = target.eulerAngles.y;
        _targetYaw = yaw;
        _targetPitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
        _orbitInitialized = true;
        ResetAngularVelocity();
    }

    private void Update()
    {
        InitializeOrbitAngles();

        bool useTouchInput = G.Control != null && G.Control.UseTouchControl && G.Input != null;
        bool pressedThisFrame = !useTouchInput && rotateOnRightMouse && Input.GetMouseButtonDown(1);
        bool canRotate = useTouchInput || !rotateOnRightMouse || Input.GetMouseButton(1);

        if (pressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ResetAngularVelocity();
        }

        float rawDeltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        float stableDeltaTime = Mathf.Min(rawDeltaTime, Mathf.Max(0.001f, maxInputDeltaTime));
        bool rejectSpikeInput = freezeOnSpike && rawDeltaTime > Mathf.Max(spikeThreshold, maxInputDeltaTime);

        // The first locked-cursor frame often contains the pointer recenter delta.
        if (canRotate && !pressedThisFrame && !rejectSpikeInput)
        {
            Vector2 lookInput = useTouchInput
                ? G.Input.Rotation
                : new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

            lookInput.x = Mathf.Clamp(lookInput.x, -maxLookInputPerFrame, maxLookInputPerFrame);
            lookInput.y = Mathf.Clamp(lookInput.y, -maxLookInputPerFrame, maxLookInputPerFrame);

            _targetYaw += lookInput.x * yawSpeed * stableDeltaTime;
            _targetPitch -= lookInput.y * pitchSpeed * stableDeltaTime;
            _targetPitch = Mathf.Clamp(_targetPitch, pitchLimits.x, pitchLimits.y);
        }

        if (!useTouchInput && rotateOnRightMouse && Input.GetMouseButtonUp(1))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float zoomDelta = -scroll * (_desiredDistance * 0.5f + 1f) * zoomSpeed;
            _desiredDistance = Mathf.Clamp(_desiredDistance + zoomDelta, minDistance, maxDistance);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        InitializeOrbitAngles();
        float stableDeltaTime = Mathf.Min(
            Mathf.Max(0.0001f, Time.unscaledDeltaTime),
            Mathf.Max(0.001f, maxInputDeltaTime));

        yaw = Mathf.SmoothDampAngle(
            yaw,
            _targetYaw,
            ref _yawVelocity,
            Mathf.Max(0.01f, rotationSmoothTime),
            Mathf.Max(90f, maxAngularSpeed),
            stableDeltaTime);
        pitch = Mathf.SmoothDampAngle(
            pitch,
            _targetPitch,
            ref _pitchVelocity,
            Mathf.Max(0.01f, rotationSmoothTime),
            Mathf.Max(90f, maxAngularSpeed),
            stableDeltaTime);
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position;
        Vector3 desiredCameraPosition = pivot - rotation * Vector3.forward * _desiredDistance;
        Vector3 toCamera = desiredCameraPosition - pivot;
        float maxDistanceToCamera = toCamera.magnitude;
        Vector3 direction = maxDistanceToCamera > 0.0001f ? toCamera / maxDistanceToCamera : Vector3.back;

        bool hitSomething = TryGetNearestCameraCollision(pivot, direction, maxDistanceToCamera, out _hit);
        if (hitSomething)
        {
            float safeDistance = Mathf.Max(_hit.distance - collisionPadding, minDistance);
            _currentDistance = Mathf.Min(safeDistance, _desiredDistance);
        }
        else
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, backReturnSpeed) * stableDeltaTime);
            _currentDistance = Mathf.Lerp(_currentDistance, _desiredDistance, blend);
        }

        Vector3 finalPosition = pivot - rotation * Vector3.forward * _currentDistance;
        transform.SetPositionAndRotation(finalPosition, rotation);
    }

    private void InitializeOrbitAngles()
    {
        if (_orbitInitialized)
            return;

        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
        _targetYaw = yaw;
        _targetPitch = pitch;
        _orbitInitialized = true;
    }

    private void ResetAngularVelocity()
    {
        _yawVelocity = 0f;
        _pitchVelocity = 0f;
    }

    private bool TryGetNearestCameraCollision(Vector3 pivot, Vector3 direction, float distance, out RaycastHit nearestHit)
    {
        nearestHit = default;
        if (distance <= 0.0001f)
            return false;

        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            collisionRadius,
            direction,
            _collisionHits,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _collisionHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hit.distance <= 0.001f || ShouldIgnoreCameraCollision(hitCollider))
                continue;
            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestHit = hit;
            found = true;
        }

        return found;
    }

    private bool ShouldIgnoreCameraCollision(Collider hitCollider)
    {
        if (target != null && hitCollider.transform.root == target.root)
            return true;

        return ignoreEggsInCollision && hitCollider.GetComponentInParent<Egg>() != null;
    }

    public void ResetCamera()
    {
        if (target == null)
            return;

        yaw = target.eulerAngles.y;
        _targetYaw = yaw;
        pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
        _targetPitch = pitch;
        _desiredDistance = Mathf.Clamp(Vector3.Distance(transform.position, target.position), minDistance, maxDistance);
        _currentDistance = _desiredDistance;
        _orbitInitialized = true;
        ResetAngularVelocity();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(target.position, collisionRadius);
    }
#endif
}

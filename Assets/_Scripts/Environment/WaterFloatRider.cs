using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class WaterFloatRider : MonoBehaviour
{
    [SerializeField] private bool affectOnlyLocalPlayer = true;
    [SerializeField] private float enterDepth = 0.15f;
    [SerializeField] private float exitHeight = 1.25f;
    [SerializeField] private float neckDepth = 1.5f;
    [SerializeField] private float kneeDepth = 0.62f;
    [SerializeField] private float depthTolerance = 0.06f;
    [SerializeField] private float sinkVelocity = 4.85f;
    [SerializeField] private float riseVelocity = 2.55f;
    [SerializeField] private float depthSpring = 5.15f;
    [SerializeField] private float floatDamping = 1.55f;
    [SerializeField] private float bobAmplitude = 0.015f;
    [SerializeField] private float bobFrequency = 0.9f;
    [SerializeField] private float wallProbeHeight = 0.95f;
    [SerializeField] private float wallProbeRadius = 0.35f;
    [SerializeField] private float wallProbeDistance = 1.25f;
    [SerializeField] private LayerMask wallMask = ~0;
    [SerializeField] private float wallJumpVerticalVelocity = 9f;
    [SerializeField] private float wallJumpHorizontalVelocity = 4.25f;
    [SerializeField] private float wallJumpCooldown = 0.85f;
    [SerializeField] private float wallJumpFloatSuspendTime = 0.65f;
    [SerializeField] private float waterJumpVerticalVelocity = 4.5f;
    [SerializeField] private float waterJumpHorizontalVelocity = 1.35f;
    [SerializeField] private float waterJumpCooldown = 0.45f;
    [SerializeField] private float waterJumpFloatSuspendTime = 0.28f;

    private readonly RaycastHit[] _wallHits = new RaycastHit[8];
    private CharacterController _controller;
    private TPPlayerController _playerController;
    private PlayerManager _playerManager;
    private bool _inWater;
    private bool _completedFirstSink;
    private float _floatPhase;
    private float _floatVelocity;
    private float _nextWallJumpTime;
    private float _nextWaterJumpTime;
    private float _suspendFloatUntil;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _playerController = GetComponent<TPPlayerController>();
        _playerManager = GetComponent<PlayerManager>();
    }

    private void LateUpdate()
    {
        if (_controller == null || !_controller.enabled)
            return;

        if (affectOnlyLocalPlayer && G.Player != null && _playerManager != G.Player)
            return;

        if (!WaterSurface.TryFindBestContaining(transform.position, out WaterSurface water))
        {
            ResetWaterState();
            return;
        }

        float surfaceY = water.SurfaceY;
        float rootY = transform.position.y;
        bool shouldEnter = rootY <= surfaceY + enterDepth;
        bool shouldStay = _inWater && rootY <= surfaceY + exitHeight;
        if (!shouldEnter && !shouldStay)
        {
            ResetWaterState();
            return;
        }

        if (!_inWater)
        {
            _completedFirstSink = false;
            _floatPhase = Mathf.PI;
            _floatVelocity = 0f;
        }

        _inWater = true;
        if (TryWaterJump())
            return;

        if (Time.time < _suspendFloatUntil)
            return;

        ApplyFloat(water);
        TryWallJump();
    }

    private void ApplyFloat(WaterSurface water)
    {
        float surfaceY = water.SurfaceY;
        float rootY = transform.position.y;
        float currentDepth = surfaceY - rootY;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        if (!_completedFirstSink)
        {
            if (currentDepth < neckDepth - depthTolerance)
            {
                _floatVelocity = Mathf.MoveTowards(_floatVelocity, -sinkVelocity, depthSpring * dt);
                SetWaterVerticalVelocity(_floatVelocity);
                return;
            }

            _completedFirstSink = true;
            _floatPhase = Mathf.PI;
            _floatVelocity = Mathf.Min(_floatVelocity, 0f);
        }

        _floatPhase += bobFrequency * dt;

        float phase01 = Mathf.PingPong(_floatPhase / Mathf.PI, 1f);
        float targetDepth = Mathf.Lerp(kneeDepth, neckDepth, phase01);
        float bob = Mathf.Sin(_floatPhase * 2.37f + GetInstanceID() * 0.13f) * bobAmplitude;
        float targetY = surfaceY - targetDepth + bob;
        float delta = targetY - rootY;

        _floatVelocity += delta * depthSpring * dt;
        _floatVelocity = Mathf.Lerp(_floatVelocity, 0f, 1f - Mathf.Exp(-floatDamping * dt));
        _floatVelocity = Mathf.Clamp(_floatVelocity, -sinkVelocity, riseVelocity);

        SetWaterVerticalVelocity(_floatVelocity);
    }

    private void SetWaterVerticalVelocity(float velocity)
    {
        if (_playerController != null)
            _playerController.OverrideVerticalVelocityForNextFrame(velocity);
        else
            _controller.Move(Vector3.up * (velocity * Time.deltaTime));
    }

    private void TryWallJump()
    {
        if (Time.time < _nextWallJumpTime)
            return;

        Vector3 direction = GetProbeDirection();
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 origin = transform.position + Vector3.up * wallProbeHeight;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            wallProbeRadius,
            direction,
            _wallHits,
            wallProbeDistance,
            wallMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _wallHits[i].collider;
            if (col == null || col.transform.IsChildOf(transform))
                continue;

            if (col.GetComponentInParent<WaterSurface>() != null)
                continue;

            if (Vector3.Dot(_wallHits[i].normal, Vector3.up) > 0.65f)
                continue;

            JumpOut(direction);
            return;
        }
    }

    private bool TryWaterJump()
    {
        if (Time.time < _nextWaterJumpTime || !IsJumpTriggered())
            return false;

        _nextWaterJumpTime = Time.time + waterJumpCooldown;
        _suspendFloatUntil = Mathf.Max(_suspendFloatUntil, Time.time + waterJumpFloatSuspendTime);
        _completedFirstSink = true;
        _floatVelocity = waterJumpVerticalVelocity;

        Vector3 direction = GetProbeDirection();
        if (_playerController != null)
        {
            _playerController.SetVerticalVelocity(waterJumpVerticalVelocity, false);

            if (direction.sqrMagnitude > 0.0001f && waterJumpHorizontalVelocity > 0f)
                _playerController.AddExternalHorizontalVelocity(direction * waterJumpHorizontalVelocity);
        }
        else
        {
            _controller.Move(Vector3.up * (waterJumpVerticalVelocity * Time.deltaTime));
        }

        return true;
    }

    private Vector3 GetProbeDirection()
    {
        Vector3 direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return direction.normalized;
    }

    private void JumpOut(Vector3 direction)
    {
        _nextWallJumpTime = Time.time + wallJumpCooldown;
        _suspendFloatUntil = Time.time + wallJumpFloatSuspendTime;
        _floatVelocity = wallJumpVerticalVelocity;

        if (_playerController != null)
        {
            _playerController.SetVerticalVelocity(wallJumpVerticalVelocity, false);
            _playerController.AddExternalHorizontalVelocity(direction * wallJumpHorizontalVelocity, true);
        }
        else
        {
            _controller.Move((Vector3.up * 0.2f + direction * 0.08f));
        }
    }

    private bool IsJumpTriggered()
    {
        if (G.Input != null)
            return G.Input.JumpTriggered;

        return Input.GetKeyDown(KeyCode.Space);
    }

    private void ResetWaterState()
    {
        _inWater = false;
        _completedFirstSink = false;
        _floatPhase = 0f;
        _floatVelocity = 0f;
        _suspendFloatUntil = 0f;
    }
}

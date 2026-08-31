using UnityEngine;

/// <summary>
/// TPS контроллер с анимациями через Animator:
/// - BlendTree по Speed (Idle/Run или IdleEgg/IdleRun при IsHolding)
/// - Переключение "держать" по булю и, опционально, по триггеру для красивого входного перехода.
/// Оптимизировано под WebGL: без лишних аллокаций, Animator-хеши кэшируются.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TPPlayerController : MonoBehaviour
{
    // === Movement ===
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 7f;
    [SerializeField] private float runSpeed = 12f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float rotationLerp = 12f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float externalVelocityDamping = 14f;

    [Header("Physics")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStick = -2f;
    [SerializeField] private float maxFrameDeltaTime = 0.05f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform; // Камера для направления движения
    [Header("Audio")]
    [SerializeField] private float footstepMinSpeed = 1.2f;
    [SerializeField] private float footstepProbeDistance = 1.5f;
    [SerializeField] private float footstepEventMinInterval = 0.075f;

    // === Animation ===
    public enum AnimParamName
    {
        Speed,       // float
        IsHolding,   // bool
        HoldTrigger  // trigger (опц.)
    }

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float speedDampTime = 0.08f;  // сглаживание параметра Speed
    [SerializeField] private float animatorSpeedMultiplier = 2f;
    [SerializeField] private float sprintAnimatorSpeedMultiplier = 3f;
    //[SerializeField] private bool useHoldTriggerOnToggle = true; // жать триггер при смене hold
    //[SerializeField] private bool debugToggleHoldWithKey = false;
    //[SerializeField] private KeyCode debugHoldKey = KeyCode.E;

    private CharacterController _cc;
    private float _verticalVel;
    private Vector3 _externalHorizontalVelocity;
    private bool _hasVerticalVelocityOverride;
    private float _verticalVelocityOverride;
    private float _currentSpeed;
    private bool _isHolding; // текущее логическое состояние "держать"
    private bool _wasGrounded;
    private float _nextFootstepAt;
    //private bool _teleportiong;
    //private Transform _teleportPoint;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _wasGrounded = _cc.isGrounded;

        //if (cameraTransform == null && Camera.main != null)
        //    cameraTransform = Camera.main.transform;

        // На всякий случай выключим root motion (контроль у CharacterController)
        ApplyAnimatorSettings();
    }

    private void ApplyAnimatorSettings()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        animator.speed = Mathf.Max(0.01f, animatorSpeedMultiplier);
    }

    public void SetCamera(Transform camera)
    {
        if (camera == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        else cameraTransform = camera;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0.001f, maxFrameDeltaTime));
        Vector3 movement;
        float h;
        float v;
        bool running;
        // ===== Ввод =====
        if (G.Input == null)
        {
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
            running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
        else
        {
            movement = G.Input.Movement;
            h = movement.x;
            v = movement.z;
            running = G.Input.Sprint;
        }

        // ===== Направление по камере =====
        Vector3 camForward = cameraTransform ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight = cameraTransform ? cameraTransform.right : Vector3.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();
    
        Vector3 moveDir = camForward * v + camRight * h;
        
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // ===== Скорость (плавно) =====
        float targetSpeed = (running ? runSpeed : walkSpeed) * moveDir.magnitude;
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * dt);

        Vector3 velocity = moveDir * _currentSpeed;

        // ===== Гравитация =====
        if (_hasVerticalVelocityOverride)
        {
            _verticalVel = _verticalVelocityOverride;
            _hasVerticalVelocityOverride = false;
        }
        else if (_cc.isGrounded)
        {
            if (_verticalVel < 0f) _verticalVel = groundedStick;
            if (G.Input && G.Input.JumpTriggered)
            {
                _verticalVel = jumpForce; // Применяем силу прыжка
            }
        }
        else
        {
            _verticalVel += gravity * dt;
        }
        velocity.y = _verticalVel;
        velocity += _externalHorizontalVelocity;

        // ===== Поворот к движению =====
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * dt);
        }

        // ===== Движение =====
        float verticalVelocityBeforeMove = _verticalVel;
        _cc.Move(velocity * dt);
        bool groundedNow = _cc.isGrounded;
        if (!_wasGrounded && groundedNow && verticalVelocityBeforeMove < -2.5f)
        {
            float landingVolume = Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(2.5f, 16f, -verticalVelocityBeforeMove));
            G.Sound?.PlayAt(GameAudioId.SFX_PLAYER_LAND, transform.position, landingVolume);
        }

        _wasGrounded = groundedNow;
        _externalHorizontalVelocity = Vector3.MoveTowards(
            _externalHorizontalVelocity,
            Vector3.zero,
            externalVelocityDamping * dt);

        // ===== Анимация =====
        if (animator != null)
        {
            animator.speed = Mathf.Max(
                0.01f,
                running ? sprintAnimatorSpeedMultiplier : animatorSpeedMultiplier);
            // Нормализуем скорость в [0..1] относительно runSpeed (один и тот же BlendTree param для обычного/hold набора)
            float normalized = runSpeed > 0.0001f ? (_currentSpeed / runSpeed) : 0f;
            animator.SetFloat(AnimParamName.Speed.ToString(), normalized, speedDampTime, dt);
            animator.SetBool(AnimParamName.IsHolding.ToString(), _isHolding);
        }
    }

    /// <summary>
    /// Установить состояние "держать". Поддерживает триггер для входного/выходного перехода.
    /// </summary>
    public void SetHolding(bool holding)
    {
        //if (_isHolding == holding) return;
        _isHolding = holding;

        if (animator != null)
        {
            animator.SetBool(AnimParamName.IsHolding.ToString(), _isHolding);

            // Если нужен отдельный переходный клип (взять/убрать предмет) — дёрнем триггер
            //if (useHoldTriggerOnToggle)
            //    animator.SetTrigger(AnimParamName.HoldTrigger.ToString());
        }
    }

    /// <summary>Удобный вызов из других скриптов (или через UnityEvent).</summary>
    public void ToggleHolding() => SetHolding(!_isHolding);

    public float VerticalVelocity => _verticalVel;

    public void SetVerticalVelocity(float velocity, bool onlyIfGreater = true)
    {
        if (onlyIfGreater && velocity <= _verticalVel)
            return;

        _hasVerticalVelocityOverride = false;
        _verticalVel = velocity;
    }

    public void OverrideVerticalVelocityForNextFrame(float velocity)
    {
        _verticalVel = velocity;
        _verticalVelocityOverride = velocity;
        _hasVerticalVelocityOverride = true;
    }

    public void AddExternalHorizontalVelocity(Vector3 velocity, bool replaceCurrent = false)
    {
        velocity.y = 0f;
        if (replaceCurrent)
            _externalHorizontalVelocity = velocity;
        else
            _externalHorizontalVelocity += velocity;
    }

    public void ResetMotion()
    {
        _verticalVel = 0f;
        _verticalVelocityOverride = 0f;
        _hasVerticalVelocityOverride = false;
        _externalHorizontalVelocity = Vector3.zero;
        _currentSpeed = 0f;
    }

    public void OnFootstepAnimationEvent()
    {
        if (_cc == null || !_cc.isGrounded || _currentSpeed < footstepMinSpeed || Time.time < _nextFootstepAt)
            return;

        float speedRatio = Mathf.Clamp01(_currentSpeed / Mathf.Max(0.01f, runSpeed));
        _nextFootstepAt = Time.time + Mathf.Max(0.04f, footstepEventMinInterval);
        G.Sound?.PlayAt(ResolveFootstepCue(), transform.position, Mathf.Lerp(0.75f, 1f, speedRatio));
    }

    private GameAudioId ResolveFootstepCue()
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                Mathf.Max(0.5f, footstepProbeDistance),
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            return GameAudioId.SFX_STEP_GRASS;
        }

        string surfaceName = hit.collider != null ? hit.collider.name.ToLowerInvariant() : string.Empty;
        Renderer renderer = hit.collider != null ? hit.collider.GetComponentInParent<Renderer>() : null;
        if (renderer != null && renderer.sharedMaterial != null)
            surfaceName += " " + renderer.sharedMaterial.name.ToLowerInvariant();

        return surfaceName.Contains("road") ||
               surfaceName.Contains("stone") ||
               surfaceName.Contains("concrete") ||
               surfaceName.Contains("tile") ||
               surfaceName.Contains("wood") ||
               surfaceName.Contains("floor") ||
               surfaceName.Contains("path") ||
               surfaceName.Contains("platform")
            ? GameAudioId.SFX_STEP_HARD
            : GameAudioId.SFX_STEP_GRASS;
    }

}

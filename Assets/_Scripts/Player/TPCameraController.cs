using UnityEngine;

/// <summary>
/// Камера 3-го лица с зумом и анти-клиппингом через SphereCast.
/// Оптимизировано: LateUpdate, без лишних аллокаций, игнор триггеров и слоя игрока.
/// Управление: ПКМ удерживать для вращения, колесо мыши — зум.
/// </summary>
[DefaultExecutionOrder(50)]
public class TPCameraController : MonoBehaviour
{
    [Header("Target & Orbit")]
    [SerializeField] private Transform target;          // Пивот у головы (CameraPivot)
    [SerializeField] private float yaw = 0f;            // Горизонтальный угол (в градусах)
    [SerializeField] private float pitch = 15f;         // Вертикальный угол (в градусах)
    [SerializeField] private Vector2 pitchLimits = new Vector2(-40f, 70f);

    [Header("Sensitivity")]
    [SerializeField] private float yawSpeed = 180f;     // °/сек на 1.0 единицу Mouse X
    [SerializeField] private float pitchSpeed = 120f;   // °/сек на 1.0 единицу Mouse Y
    [SerializeField] private bool rotateOnRightMouse = true;

    [Header("Zoom")]
    [SerializeField] private float minDistance = 1.6f;
    [SerializeField] private float maxDistance = 6.0f;
    [SerializeField] private float zoomSpeed = 3.0f;    // Чем больше, тем быстрее реакция на колесо
    [SerializeField] private float backReturnSpeed = 8f;// Скорость возврата к желаемой дистанции без препятствий

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionPadding = 0.1f;
    [SerializeField] private LayerMask collisionMask = ~0; // Выключи тут слой Player

    private float _desiredDistance;
    private float _currentDistance;
    private Camera _cam;
    private RaycastHit _hit; // поле, чтобы не аллоцировать в стеке каждый кадр

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        //if (target == null)
        //{
        //    Debug.LogWarning("[TPCameraController] Target not set. Disabling.");
        //    enabled = false;
        //    return;
        //}

        // Инициализация дистанции от текущего положения камеры
        //_desiredDistance = Mathf.Clamp(Vector3.Distance(transform.position, target.position), minDistance, maxDistance);
        //_currentDistance = _desiredDistance;

        //// Начальный yaw берём из поворота таргета
        //yaw = target.eulerAngles.y;
    }


    public void SetTarget(Transform t)
    {
        target = t;
        _desiredDistance = Mathf.Clamp(Vector3.Distance(transform.position, target.position), minDistance, maxDistance);
        _currentDistance = _desiredDistance;

        // Начальный yaw берём из поворота таргета
        yaw = target.eulerAngles.y;
    }

    private void Update()
    {
        // Вращение — только при удержании ПКМ (удобно для WebGL), либо всегда, если отключить флаг
        bool canRotate = !rotateOnRightMouse || Input.GetMouseButton(1);

        if (canRotate)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            yaw += mx * yawSpeed * Time.deltaTime;
            pitch -= my * pitchSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);

            // Захват/освобождение курсора под WebGL — только при действий пользователя
            if (rotateOnRightMouse)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        if (rotateOnRightMouse && Input.GetMouseButtonUp(1))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Зум (колесо мыши), экспоненциально-плавный
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float zoomDelta = -scroll * (_desiredDistance * 0.5f + 1f) * zoomSpeed; // немного лог-подобного ощущения
            _desiredDistance = Mathf.Clamp(_desiredDistance + zoomDelta, minDistance, maxDistance);
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Вычисляем желаемую позицию камеры относительно таргета
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position;
        Vector3 desiredCameraPos = pivot - (rot * Vector3.forward) * _desiredDistance;

        // Анти-клиппинг: SphereCast от pivot к желаемой позиции
        Vector3 toCam = desiredCameraPos - pivot;
        float maxDist = toCam.magnitude;
        Vector3 dir = maxDist > 0.0001f ? toCam / maxDist : Vector3.back;

        bool hitSomething = Physics.SphereCast(pivot, collisionRadius, dir, out _hit, maxDist, collisionMask, QueryTriggerInteraction.Ignore);
        float targetDistance = _desiredDistance;

        if (hitSomething)
        {
            float safeDist = Mathf.Max(_hit.distance - collisionPadding, minDistance);
            targetDistance = Mathf.Min(safeDist, _desiredDistance);
            _currentDistance = targetDistance; // мгновенно поджимаем, чтобы не влезать в стену
        }
        else
        {
            // Плавно возвращаемся назад к _desiredDistance без рывков (fps-независимо)
            _currentDistance = Mathf.Lerp(_currentDistance, _desiredDistance, 1f - Mathf.Exp(-backReturnSpeed * Time.deltaTime));
        }

        // Обновляем трансформ камеры
        Vector3 finalPos = pivot - (rot * Vector3.forward) * _currentDistance;
        transform.SetPositionAndRotation(finalPos, rot);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(target.position, collisionRadius);
    }
#endif
}

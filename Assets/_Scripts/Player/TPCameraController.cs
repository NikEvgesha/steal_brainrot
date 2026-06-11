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

    [SerializeField] private float maxDistance = 18.0f;

    [SerializeField] private float zoomSpeed = 3.0f;    // Чем больше, тем быстрее реакция на колесо

    [SerializeField] private float backReturnSpeed = 8f;// Скорость возврата к желаемой дистанции без препятствий



    [Header("Collision")]

    [SerializeField] private float collisionRadius = 0.25f;

    [SerializeField] private float collisionPadding = 0.1f;

    [SerializeField] private LayerMask collisionMask = ~0; // Выключи тут слой Player


    [SerializeField] private bool ignoreEggsInCollision = true;

    [Header("Smoothing")]
    [SerializeField] private bool freezeOnSpike = true;
    [SerializeField] private float spikeThreshold = 0.05f;

    private float _desiredDistance;

    private float _currentDistance;

    private Camera _cam;

    private RaycastHit _hit; // поле, чтобы не аллоцировать в стеке каждый кадр


    private readonly RaycastHit[] _collisionHits = new RaycastHit[32];




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
        bool spikeUpdate = freezeOnSpike && Time.unscaledDeltaTime > spikeThreshold;
        if (spikeUpdate)
        {
            if (rotateOnRightMouse && Input.GetMouseButtonUp(1))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }


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

        // ????????? ???????? ??????? ?????? ???????????? ???????
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position;
        Vector3 desiredCameraPos = pivot - (rot * Vector3.forward) * _desiredDistance;

        // ????-????????: SphereCast ?? pivot ? ???????? ???????
        Vector3 toCam = desiredCameraPos - pivot;
        float maxDist = toCam.magnitude;
        Vector3 dir = maxDist > 0.0001f ? toCam / maxDist : Vector3.back;

        bool hitSomething = TryGetNearestCameraCollision(pivot, dir, maxDist, out _hit);
        float targetDistance = _desiredDistance;

        if (hitSomething)
        {
            float safeDist = Mathf.Max(_hit.distance - collisionPadding, minDistance);
            targetDistance = Mathf.Min(safeDist, _desiredDistance);
            _currentDistance = targetDistance; // ????????? ?????????, ????? ?? ??????? ? ?????
        }
        else
        {
            // ?????? ???????????? ????? ? _desiredDistance ??? ?????? (fps-??????????)
            _currentDistance = Mathf.Lerp(_currentDistance, _desiredDistance, 1f - Mathf.Exp(-backReturnSpeed * Time.deltaTime));
        }

        // ????????? ????????? ??????
        Vector3 finalPos = pivot - (rot * Vector3.forward) * _currentDistance;
        transform.SetPositionAndRotation(finalPos, rot);

        // Safety: prevent roll drift
        var e = transform.eulerAngles;
        if (Mathf.Abs(e.z) > 0.01f)
            transform.rotation = Quaternion.Euler(e.x, e.y, 0f);
    }

    private bool TryGetNearestCameraCollision(Vector3 pivot, Vector3 dir, float maxDist, out RaycastHit nearestHit)
    {
        nearestHit = default(RaycastHit);

        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            collisionRadius,
            dir,
            _collisionHits,
            maxDist,
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
#if UNITY_EDITOR

    private void OnDrawGizmosSelected()

    {

        if (target == null) return;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);

        Gizmos.DrawWireSphere(target.position, collisionRadius);

    }

#endif

    public void ResetCamera()
    {
        if (target == null)
            return;

        yaw = target.eulerAngles.y;
        _desiredDistance = Mathf.Clamp(Vector3.Distance(transform.position, target.position), minDistance, maxDistance);
        _currentDistance = _desiredDistance;
    }

}



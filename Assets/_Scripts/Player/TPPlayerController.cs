using UnityEngine;

/// <summary>
/// Простой и шустрый TPS контроллер под CharacterController.
/// Движение от камеры, плавное ускорение, гравитация. Без GC-аллокаций в апдейте.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TPPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float runSpeed = 6.0f;
    [SerializeField] private float acceleration = 12f;       // Насколько быстро набираем/сбрасываем скорость
    [SerializeField] private float rotationLerp = 12f;       // Скорость разворота к направлению бега

    [Header("Physics")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStick = -2f;      // Лёгкая «прилипчивость» к земле

    [Header("References")]
    [SerializeField] private Transform cameraTransform;      // Перетащи сюда главную камеру

    private CharacterController _cc;
    private float _verticalVel;
    private float _currentSpeed;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        // 1) Считываем ввод
        //float h = Input.GetAxisRaw("Horizontal");
        //float v = Input.GetAxisRaw("Vertical");

        Vector3 movement = PlayerInput.Instance.Movement;

        //bool running = PlayerInput.Instance.Sprint; //Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // 2) Направление в плоскости XZ относительно камеры
        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 moveDir = camForward * movement.z + camRight * movement.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // 3) Плавное изменение целевой горизонтальной скорости
        float targetSpeed = /*(running ? runSpeed : walkSpeed) **/ walkSpeed * moveDir.magnitude;
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        Vector3 velocity = moveDir * _currentSpeed;

        // 4) Гравитация
        if (_cc.isGrounded)
        {
            if (_verticalVel < 0f) _verticalVel = groundedStick;
        }
        else
        {
            _verticalVel += gravity * Time.deltaTime;
        }

        velocity.y = _verticalVel;

        // 5) Поворот персонажа в сторону движения
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
        }

        // 6) Движение контроллером
        _cc.Move(velocity * Time.deltaTime);
    }
}

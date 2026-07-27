#if UNITY_EDITOR
using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed class EditorFlightCheat : MonoBehaviour
{
    private const float DoubleTapWindow = 0.32f;
    private const float SearchInterval = 0.35f;
    private const float FlySpeed = 18f;
    private const float FastFlySpeed = 32f;

    private static EditorFlightCheat _instance;

    private TPPlayerController _controller;
    private CharacterController _characterController;
    private float _lastSpaceTap = -10f;
    private float _nextSearchTime;
    private bool _flying;
    private bool _controllerWasEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (_instance != null)
            return;

        EditorFlightCheat existing = FindAnyObjectByType<EditorFlightCheat>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        GameObject bootstrap = new GameObject("Editor Flight Cheat");
        DontDestroyOnLoad(bootstrap);
        _instance = bootstrap.AddComponent<EditorFlightCheat>();
    }

    private void Update()
    {
        ResolvePlayerController();
        if (_controller == null)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            float now = Time.unscaledTime;
            if (now - _lastSpaceTap <= DoubleTapWindow)
            {
                SetFlying(!_flying);
                _lastSpaceTap = -10f;
                return;
            }

            _lastSpaceTap = now;
        }

        if (_flying)
            MoveFlying();
    }

    private void ResolvePlayerController()
    {
        if (_controller != null)
            return;
        if (Time.unscaledTime < _nextSearchTime)
            return;

        _nextSearchTime = Time.unscaledTime + SearchInterval;
        _controller = FindAnyObjectByType<TPPlayerController>();
        if (_controller == null)
            return;

        _characterController = _controller.GetComponent<CharacterController>();
    }

    private void SetFlying(bool flying)
    {
        if (_controller == null || _flying == flying)
            return;

        _flying = flying;
        if (_flying)
        {
            _controllerWasEnabled = _controller.enabled;
            _controller.enabled = false;
            Debug.Log("[EditorFlightCheat] Flight enabled. WASD to move, Space/Ctrl vertically, Shift to accelerate.");
            return;
        }

        _controller.enabled = _controllerWasEnabled;
        _controller.SetVerticalVelocity(0f, false);
        Debug.Log("[EditorFlightCheat] Flight disabled.");
    }

    private void MoveFlying()
    {
        Camera camera = Camera.main;
        Transform cameraTransform = camera != null ? camera.transform : null;
        Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        float lift = 0f;
        if (Input.GetKey(KeyCode.Space))
            lift += 1f;
        if (Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.C))
        {
            lift -= 1f;
        }

        Vector3 direction = forward * vertical + right * horizontal + Vector3.up * lift;
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        Vector3 motion = direction * (fast ? FastFlySpeed : FlySpeed) * Time.unscaledDeltaTime;
        if (_characterController != null && _characterController.enabled)
            _characterController.Move(motion);
        else
            _controller.transform.position += motion;

        Vector3 horizontalDirection = new Vector3(direction.x, 0f, direction.z);
        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
            _controller.transform.rotation = Quaternion.Slerp(
                _controller.transform.rotation,
                targetRotation,
                12f * Time.unscaledDeltaTime);
        }
    }

    private void OnGUI()
    {
        if (!_flying)
            return;

        GUI.Label(new Rect(16f, 16f, 330f, 28f), "EDITOR FLY: WASD / Space / Ctrl / Shift");
    }

    private void OnDestroy()
    {
        if (_flying && _controller != null)
            SetFlying(false);
        if (_instance == this)
            _instance = null;
    }
}
#endif

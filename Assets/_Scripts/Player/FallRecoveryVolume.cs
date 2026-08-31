using UnityEngine;

[DisallowMultipleComponent]
public sealed class FallRecoveryVolume : MonoBehaviour
{
    private const float DefaultDropBelowSpawn = 25f;
    private const float DefaultTriggerDepth = 10f;
    private const float DefaultWorldExtent = 5000f;
    private const float RecoveryCooldown = 1f;

    [SerializeField] private Transform fallbackPoint;
    [SerializeField] private float dropBelowSpawn = DefaultDropBelowSpawn;
    [SerializeField] private float triggerDepth = DefaultTriggerDepth;
    [SerializeField] private float worldExtent = DefaultWorldExtent;

    private BoxCollider _trigger;
    private RemoteBasesApplier _remoteBases;
    private float _recoveryHeight;
    private float _nextRecoveryAt;

    public static FallRecoveryVolume EnsureExists(Transform fallbackSpawnPoint)
    {
        var volume = FindAnyObjectByType<FallRecoveryVolume>(FindObjectsInactive.Include);
        if (volume == null)
        {
            var root = new GameObject(nameof(FallRecoveryVolume));
            volume = root.AddComponent<FallRecoveryVolume>();
        }

        if (fallbackSpawnPoint != null)
            volume.fallbackPoint = fallbackSpawnPoint;

        volume.ConfigureTrigger();
        return volume;
    }

    private void Awake()
    {
        ConfigureTrigger();
    }

    private void Update()
    {
        var player = G.Player;
        if (player != null && player.transform.position.y <= _recoveryHeight)
            Recover(player);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        var player = other.GetComponentInParent<PlayerManager>();
        if (player != null && player == G.Player)
            Recover(player);
    }

    private void ConfigureTrigger()
    {
        float referenceY = fallbackPoint != null ? fallbackPoint.position.y : 0f;
        _recoveryHeight = referenceY - Mathf.Max(5f, dropBelowSpawn);

        if (_trigger == null)
            _trigger = GetComponent<BoxCollider>();
        if (_trigger == null)
            _trigger = gameObject.AddComponent<BoxCollider>();

        _trigger.isTrigger = true;
        _trigger.center = Vector3.zero;
        _trigger.size = new Vector3(
            Mathf.Max(100f, worldExtent),
            Mathf.Max(2f, triggerDepth),
            Mathf.Max(100f, worldExtent));

        Vector3 center = fallbackPoint != null ? fallbackPoint.position : Vector3.zero;
        center.y = _recoveryHeight - _trigger.size.y * 0.5f;
        transform.position = center;
        transform.rotation = Quaternion.identity;
        gameObject.name = nameof(FallRecoveryVolume);
    }

    private void Recover(PlayerManager player)
    {
        if (player == null || Time.unscaledTime < _nextRecoveryAt)
            return;

        Transform target = ResolveRecoveryTarget();
        if (target == null)
            return;

        _nextRecoveryAt = Time.unscaledTime + RecoveryCooldown;

        var movement = player.GetComponent<TPPlayerController>();
        if (movement != null)
            movement.ResetMotion();

        G.Sound?.PlayAt(GameAudioId.SFX_TELEPORT, player.transform.position);
        player.Teleport(target);

        GameAnalytics.Track(
            AnalyticsEventNames.TeleportUsed,
            GameAnalytics.Params(
                "destination", "home",
                "destination_object", target.name,
                "source", "fall_recovery",
                "result", "success"));
    }

    private Transform ResolveRecoveryTarget()
    {
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>(FindObjectsInactive.Include);

        if (_remoteBases != null)
        {
            Transform entryPoint = _remoteBases.GetLocalSlotEntryPoint();
            if (entryPoint != null)
                return entryPoint;

            Transform localRoot = _remoteBases.GetLocalSlotRoot();
            if (localRoot != null)
                return localRoot;
        }

        return fallbackPoint;
    }
}

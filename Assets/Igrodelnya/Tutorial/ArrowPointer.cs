using UnityEngine;
using UnityEngine.AI;

public class ArrowPointer : MonoBehaviour
{
    public static ArrowPointer Instance { get; private set; }
    [Tooltip("Объект, на который нужно указывать")]
    public Transform target;

    [Tooltip("Если у персонажа есть NavMeshAgent, укажи его — стрелка будет смотреть на steeringTarget")]
    public NavMeshAgent agent;

    [Tooltip("Скорость поворота (град/сек)")]
    public float turnSpeed = 540f;

    [Tooltip("Скрывать стрелку, если подходим близко")]
    public float hideDistance = 1.0f;

    [Tooltip("Насколько прижимаем стрелку к полу")]
    public float groundRayHeight = 0.2f;

    [Tooltip("Слои, считающиеся полом")]
    public LayerMask groundMask = ~0;

    Renderer[] rends;

    void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        rends = GetComponentsInChildren<Renderer>(true);
    }

    void Update()
    {
        Vector3? tgt = GetTargetPos();
        if (!tgt.HasValue) { SetVisible(false); return; }

        Vector3 tgtPos = tgt.Value;
        float dist = Vector3.Distance(tgtPos, transform.position);
        SetVisible(dist > hideDistance);

        // Поворачиваемся к цели только по горизонтали (Y — вверх)
        Vector3 dir = tgtPos - transform.position;
        dir.y = 0f; // << ключевая строка: игнорируем высоту

        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    Vector3? GetTargetPos()
    {
        if (agent != null && agent.hasPath) return agent.steeringTarget;
        if (target != null) return target.position;
        return null;
    }

    void SetVisible(bool v)
    {
        if (rends == null) return;
        foreach (var r in rends) r.enabled = v;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
#endif
}

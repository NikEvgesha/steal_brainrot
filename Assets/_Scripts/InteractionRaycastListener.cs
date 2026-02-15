using UnityEngine;
using UnityEngine.Events;

public class InteractionRaycastListener : MonoBehaviour
{
    [SerializeField] public UnityEvent _hitEvent;
    [SerializeField] public UnityEvent _noHitEvent;

    [SerializeField] public float MaxDistance;

    private void Awake()
    {
        EnsureEvents();
    }

    private void OnValidate()
    {
        EnsureEvents();
    }

    private void OnDestroy()
    {
        _hitEvent?.RemoveAllListeners();
        _noHitEvent?.RemoveAllListeners();
    }

    public void onRaycastHit()
    {
        _hitEvent?.Invoke();
    }

    public void onRaycastFail()
    {
        _noHitEvent?.Invoke();
    }

    private void EnsureEvents()
    {
        _hitEvent ??= new UnityEvent();
        _noHitEvent ??= new UnityEvent();
    }
}

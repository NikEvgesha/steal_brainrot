using System;
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
        EnsureZoneVisual();
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

    private void EnsureZoneVisual()
    {
        if (!ShouldCreateZoneVisual())
            return;
        if (GetComponent<InteractionZoneVisual>() != null)
            return;

        gameObject.AddComponent<InteractionZoneVisual>();
    }

    private bool ShouldCreateZoneVisual()
    {
        if (GetComponent<BoxCollider>() == null)
            return false;

        for (Transform current = transform; current != null; current = current.parent)
        {
            string name = current.name;
            if (name.IndexOf("Conveyor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Shop", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

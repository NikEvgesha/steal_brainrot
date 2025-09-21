using UnityEngine;
using UnityEngine.Events;

public class InteractionRaycastListener : MonoBehaviour
{
    [SerializeField] public UnityEvent _hitEvent;
    [SerializeField] public UnityEvent _noHitEvent;


    public void onRaycastHit()
    {
        _hitEvent.Invoke();
    }

    public void onRaycastFail()
    {
        _noHitEvent.Invoke();
    }
}

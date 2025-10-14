using UnityEngine;

public class QuestTrigger : MonoBehaviour
{
    [SerializeField] InteractType _interactType;
    private void OnTriggerEnter(Collider other)
    {
        /*if (other.GetComponent<PlayerManager>())
            GameEvents.OnNPCInteracted?.Invoke(_interactType);*/
    }
}

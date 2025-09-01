using UnityEngine;

/// <summary>
/// Условие «взаимодействуй (Interact) с NPC/объектом»
/// Допустим, в вашем игровом коде при взаимодействии вызывают:
///     GameEvents.OnNPCInteracted(npcId);
/// </summary>
public class InteractConditionComponent : MonoBehaviour, IQuestCondition
{
    [Header("Параметры условия")]
    [Tooltip("ID NPC или объекта, с которым нужно взаимодействовать")]
    public InteractType targetNpcId;

    // Если нужно лишь однократное взаимодействие, 
    // то при первом вызове считаем условие выполненным
    private bool interacted = false;

    public bool IsSatisfied => interacted;

    public void Initialize()
    {
        GameEvents.OnNPCInteracted += OnNPCInteracted;
    }

    private void OnNPCInteracted(InteractType npcId)
    {
        if (!interacted && npcId == targetNpcId)
        {
            interacted = true;
        }
    }

    public float GetProgressNormalized()
    {
        return interacted ? 1f : 0f;
    }

    public void Dispose()
    {
        GameEvents.OnNPCInteracted -= OnNPCInteracted;
    }

    private void OnDisable()
    {
        GameEvents.OnNPCInteracted -= OnNPCInteracted;
    }
}

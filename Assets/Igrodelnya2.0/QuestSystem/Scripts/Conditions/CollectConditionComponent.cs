using UnityEngine;

/// <summary>
/// Условие «собрать X предметов с данным ID».
/// </summary>
public class CollectConditionComponent : MonoBehaviour, IQuestCondition
{
    [Header("Параметры условия")]
    [Tooltip("ID предмета, который нужно собрать")]
    public ItemTag targetItemId;

    [Tooltip("Сколько штук предмета нужно собрать")]
    public int requiredCount = 1;

    private int currentCount = 0;

    public bool IsSatisfied => currentCount >= requiredCount;

    public void Initialize()
    {
        GameEvents.OnItemCollected += OnItemCollected;
    }

    private void OnItemCollected(ItemTag itemId)
    {
        if (itemId == targetItemId)
        {
            currentCount = Mathf.Min(currentCount + 1, requiredCount);
        }
    }

    public float GetProgressNormalized()
    {
        if (requiredCount <= 0) return 1f;
        return Mathf.Clamp01((float)currentCount / requiredCount);
    }

    public void Dispose()
    {
        GameEvents.OnItemCollected -= OnItemCollected;
    }

    private void OnDisable()
    {
        GameEvents.OnItemCollected -= OnItemCollected;
    }
}

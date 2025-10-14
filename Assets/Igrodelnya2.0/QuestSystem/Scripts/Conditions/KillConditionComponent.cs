using UnityEngine;

/// <summary>
/// Условие «убей X штук врага с данным ID».
/// </summary>
public class KillConditionComponent : MonoBehaviour, IQuestCondition
{
    [Header("Параметры условия")]
    [Tooltip("ID врага, которого нужно убить")]
    public EnemyType targetEnemyId;

    [Tooltip("Сколько штук нужно убить")]
    public int requiredCount = 1;

    // Внутренний счётчик
    private int currentCount = 0;

    public bool IsSatisfied => currentCount >= requiredCount;

    public void Initialize()
    {
        // Подписываемся на статическое событие, когда где-то в игре враг умирает
        GameEvents.OnEnemyKilled += OnEnemyKilled;
    }

    private void OnEnemyKilled(EnemyType enemyId)
    {
        enemyId = targetEnemyId == EnemyType.Any? EnemyType.Any : enemyId;
        
        if (enemyId == targetEnemyId)
        {
            currentCount = Mathf.Min(currentCount + 1, requiredCount);
            // Можно здесь же вызвать событие «обновился прогресс»,
            // если захотите отказаться от Update() в QuestInstance
        }
    }

    public float GetProgressNormalized()
    {
        if (requiredCount <= 0) return 1f;
        return Mathf.Clamp01((float)currentCount / requiredCount);
    }

    public void Dispose()
    {
        // Отписаться, чтобы не было утечки
        GameEvents.OnEnemyKilled -= OnEnemyKilled;
    }

    // Обязательно убеждаемся, что отписались, если объект уничтожили
    private void OnDisable()
    {
        GameEvents.OnEnemyKilled -= OnEnemyKilled;
    }
}

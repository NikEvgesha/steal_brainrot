using System;

/// <summary>
/// Статические события, на которые подписываются компоненты условий квестов.
/// В вашем игровом коде вы должны при нужных ситуациях вызывать эти события:
///   GameEvents.OnEnemyKilled?.Invoke(enemyId);
///   GameEvents.OnItemCollected?.Invoke(itemId);
///   GameEvents.OnNPCInteracted?.Invoke(npcId);
/// </summary>
public static class GameEvents
{
    /// <summary>
    /// Вызывается, когда в игре умирает враг.
    /// Параметр: ID врага (int).
    /// </summary>
    public static Action<EnemyType> OnEnemyKilled;

    /// <summary>
    /// Вызывается, когда игрок подбирает предмет.
    /// Параметр: ID предмета (int).
    /// </summary>
    public static Action<ItemTag> OnItemCollected;

    /// <summary>
    /// Вызывается, когда игрок взаимодействует с NPC/объектом.
    /// Параметр: ID NPC (int).
    /// </summary>
    public static Action<InteractType> OnNPCInteracted;
}

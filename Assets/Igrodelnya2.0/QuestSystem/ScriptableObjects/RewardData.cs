using UnityEngine;

// Очень простой ScriptableObject, в котором вы можете хранить данные о награде.
// Например, количество монет, опыт, предмет и т. д.
[CreateAssetMenu(menuName = "Igrodelnya/QuestSystem/Reward Data", fileName = "NewRewardData")]
public class RewardData : ScriptableObject
{
    [Header("Пример: валюты/опыт")]
    public int coins = 0;              // сколько «монет» дать игроку
    public int experience = 0;         // сколько опыта

    [Header("При необходимости: ItemID, предмет и т.д.")]
    public string itemId;              // если нужно выдать предмет (например, строковый ID)

    // Можно добавить любые другие поля: скилл-поинты, редкий предмет, 
    // или просто использовать эту SO, чтобы прокинуть числа в другое место через RewardManager.
}

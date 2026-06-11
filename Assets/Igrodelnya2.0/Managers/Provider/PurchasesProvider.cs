using System;
using UnityEngine;


// Интерфейс для провайдеров покупок
public abstract class PurchasesProvider : MonoBehaviour
{
    public abstract bool IsInitialized { get; }
    public abstract void Initialize(); // Инициализация провайдера
    public abstract void BuyPurchase(string purchaseId, Action<bool> onComplete); // Вызов покупки с коллбэком
    public abstract void ConsumePendingPurchases(); // Обработка необработанных покупок
    public abstract PurchaseData GetPurchaseData(string purchaseId); // Получение данных о покупке

    public abstract bool PurchasesAvailable();
}

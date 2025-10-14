using System.Collections.Generic;
using UnityEngine;

// Интерфейс для аналитических провайдеров
public abstract class AnalyticsProvider : MonoBehaviour
{
    public abstract void Initialize(); // Инициализация провайдера
    public abstract void SendEvent(string eventName, Dictionary<string, object> parameters = null); // Отправка события
    public abstract void SendEvent(string eventName, Dictionary<string, string> parameters = null); // Отправка события
    public abstract void SendEvent(string eventName); // Отправка события
}

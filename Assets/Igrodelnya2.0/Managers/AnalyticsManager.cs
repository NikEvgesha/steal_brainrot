using UnityEngine;
using System.Collections.Generic;

// Главный менеджер аналитики
public class AnalyticsManager : MonoBehaviour
{
    private static AnalyticsManager _instance;
    public static AnalyticsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AnalyticsManager");
                _instance = go.AddComponent<AnalyticsManager>();
                DontDestroyOnLoad(go); // Чтобы менеджер сохранялся между сценами
            }
            return _instance;
        }
    }

    [SerializeField] private List<AnalyticsProvider> analyticsProviders = new List<AnalyticsProvider>(); // Список активных провайдеров

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        //DontDestroyOnLoad(gameObject);

        // Инициализация всех провайдеров
        InitializeProviders();
        //LogEvent(EventName.gameStart.ToString());
    }

    private void InitializeProviders()
    {
        foreach (var provider in analyticsProviders)
        {
            provider.Initialize();
            //Debug.Log($"Initialized analytics provider: {provider.GetType().Name}");
        }
    }

    // Метод для отправки события всем провайдерам
    public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        if (analyticsProviders.Count == 0)
        {
            Debug.LogWarning("No analytics providers configured!");
            return;
        }

        foreach (var provider in analyticsProviders)
        {
            provider.SendEvent(eventName, parameters);
        }
    }
    public void LogEvent(string eventName)
    {
        if (analyticsProviders.Count == 0)
        {
            Debug.LogWarning("No analytics providers configured!");
            return;
        }

        foreach (var provider in analyticsProviders)
        {
            provider.SendEvent(eventName);
        }
    }
    public void LogEvent(string eventName, Dictionary<string, string> parameters = null)
    {
        if (analyticsProviders.Count == 0)
        {
            Debug.LogWarning("No analytics providers configured!");
            return;
        }

        foreach (var provider in analyticsProviders)
        {
            provider.SendEvent(eventName, parameters);
        }
    }
    // Метод для добавления провайдера в рантайме (опционально)
    public void AddProvider(AnalyticsProvider provider)
    {
        if (!analyticsProviders.Contains(provider))
        {
            analyticsProviders.Add(provider);
            provider.Initialize();
        }
    }
}

// Пример реализации для Firebase Analytics
/*public class FirebaseAnalyticsProvider : IAnalyticsProvider
{
    public void Initialize()
    {
        // Здесь код инициализации Firebase, если нужно
        Debug.Log("Firebase Analytics initialized");
    }

    public void SendEvent(string eventName, Dictionary<string, object> parameters)
    {
        // Пример отправки события в Firebase
        // Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, parameters);
        Debug.Log($"Firebase event logged: {eventName}, Params: {parameters?.Count ?? 0}");
    }
}*/


using System.Collections.Generic;
using UnityEngine;

// Central fan-out for product analytics providers.
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
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    [SerializeField] private List<AnalyticsProvider> analyticsProviders = new List<AnalyticsProvider>();
    private bool _missingProvidersWarningLogged;

    public bool HasProviders => analyticsProviders != null && analyticsProviders.Count > 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        InitializeProviders();
    }

    private void InitializeProviders()
    {
        foreach (AnalyticsProvider provider in analyticsProviders)
            provider.Initialize();
    }

    public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!HasProviders)
        {
            WarnAboutMissingProvidersOnce();
            return;
        }

        foreach (AnalyticsProvider provider in analyticsProviders)
            provider.SendEvent(eventName, parameters);
    }

    public void LogEvent(string eventName)
    {
        if (!HasProviders)
        {
            WarnAboutMissingProvidersOnce();
            return;
        }

        foreach (AnalyticsProvider provider in analyticsProviders)
            provider.SendEvent(eventName);
    }

    public void LogEvent(string eventName, Dictionary<string, string> parameters = null)
    {
        if (!HasProviders)
        {
            WarnAboutMissingProvidersOnce();
            return;
        }

        foreach (AnalyticsProvider provider in analyticsProviders)
            provider.SendEvent(eventName, parameters);
    }

    public void AddProvider(AnalyticsProvider provider)
    {
        if (!analyticsProviders.Contains(provider))
        {
            analyticsProviders.Add(provider);
            provider.Initialize();
        }
    }

    private void WarnAboutMissingProvidersOnce()
    {
        if (_missingProvidersWarningLogged)
            return;

        _missingProvidersWarningLogged = true;
        Debug.LogWarning("No analytics providers configured. Product events will be ignored in this session.");
    }
}

// Example Firebase provider implementation.
/*public class FirebaseAnalyticsProvider : IAnalyticsProvider
{
    public void Initialize()
    {
        // Initialize Firebase here when the provider is enabled.
        Debug.Log("Firebase Analytics initialized");
    }

    public void SendEvent(string eventName, Dictionary<string, object> parameters)
    {
        // Forward the event to Firebase.
        // Firebase.Analytics.FirebaseAnalytics.LogEvent(eventName, parameters);
        Debug.Log($"Firebase event logged: {eventName}, Params: {parameters?.Count ?? 0}");
    }
}*/

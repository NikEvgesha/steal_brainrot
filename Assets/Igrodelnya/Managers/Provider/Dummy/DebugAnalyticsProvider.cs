using System.Collections.Generic;
using UnityEngine;
// Пример реализации для отладочной аналитики
public class DebugAnalyticsProvider : AnalyticsProvider
{
    public override void Initialize()
    {
        Debug.Log("Debug Analytics initialized");
    }

    public override void SendEvent(string eventName, Dictionary<string, object> parameters)
    {
        string paramString = parameters != null ? string.Join(", ", parameters) : "None";
        Debug.Log($"Debug event: {eventName}, Parameters: {paramString}");
    }
    public override void SendEvent(string eventName, Dictionary<string, string> parameters)
    {
        string paramString = parameters != null ? string.Join(", ", parameters) : "None";
        Debug.Log($"Debug event: {eventName}, Parameters: {paramString}");
    }
    public override void SendEvent(string eventName)
    {
        Debug.Log($"Debug event: {eventName}");
    }
}
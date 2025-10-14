using System.Collections.Generic;
using UnityEngine;
using MirraGames.SDK;  // Пространство имён MirraSDK

//#if MIRRA_SDK_ENABLED
public class MirraSDKAnalyticsProvider : AnalyticsProvider
{
    /// <summary>
    /// Ждём, пока EventsReporter инициализируется,
    /// чтобы не потерять первые события.
    /// </summary>
    public override void Initialize()
    {
        MirraSDK.WaitForProviders(() =>
        {
            //Debug.Log("MirraSDKAnalyticsProvider: Events reporter initialized");
        });  // :contentReference[oaicite:0]{index=0}
    }

    /// <summary>
    /// Отправка простого события без параметров.
    /// </summary>
    public override void SendEvent(string eventName)
    {
        if (MirraSDK.Analytics.IsEventsReporterInitialized)
        {
            MirraSDK.Analytics.Report(eventName);
            Debug.Log($"MirraSDKAnalyticsProvider: Event sent: {eventName}");
        }
        else
        {
            Debug.LogWarning($"MirraSDKAnalyticsProvider: Reporter not ready, skipped event '{eventName}'");
        }
    }

    /// <summary>
    /// Отправка события с произвольными параметрами (object).
    /// </summary>
    public override void SendEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (MirraSDK.Analytics.IsEventsReporterInitialized)
        {
            MirraSDK.Analytics.Report(eventName, parameters);
            Debug.Log($"MirraSDKAnalyticsProvider: Event sent: {eventName} with parameters");
        }
        else
        {
            Debug.LogWarning($"MirraSDKAnalyticsProvider: Reporter not ready, skipped event '{eventName}'");
        }
    }

    /// <summary>
    /// Отправка события с параметрами-строками.
    /// Просто конвертируем их в object-словарь.
    /// </summary>
    public override void SendEvent(string eventName, Dictionary<string, string> parameters)
    {
        if (MirraSDK.Analytics.IsEventsReporterInitialized)
        {
            var objParams = new Dictionary<string, object>(parameters.Count);
            foreach (var kv in parameters)
                objParams[kv.Key] = kv.Value;

            MirraSDK.Analytics.Report(eventName, objParams);
            Debug.Log($"MirraSDKAnalyticsProvider: Event sent: {eventName} with string parameters");
        }
        else
        {
            Debug.LogWarning($"MirraSDKAnalyticsProvider: Reporter not ready, skipped event '{eventName}'");
        }
    }
}
//#endif

using System;
using System.Collections.Generic;
using MirraGames.SDK;
using UnityEngine;

public sealed class MirraSDKAnalyticsProvider : AnalyticsProvider
{
    public override bool IsReady
    {
        get
        {
            try
            {
                return MirraSDK.Analytics.IsEventsReporterInitialized;
            }
            catch
            {
                return false;
            }
        }
    }

    public override void Initialize()
    {
        MirraSDK.WaitForProviders(() => { });
    }

    public override void SendEvent(string eventName)
    {
        TrySendEvent(eventName, null);
    }

    public override void SendEvent(string eventName, Dictionary<string, object> parameters)
    {
        TrySendEvent(eventName, parameters);
    }

    public override void SendEvent(string eventName, Dictionary<string, string> parameters)
    {
        var converted = new Dictionary<string, object>();
        if (parameters != null)
        {
            foreach (KeyValuePair<string, string> pair in parameters)
                converted[pair.Key] = pair.Value;
        }
        TrySendEvent(eventName, converted);
    }

    public override bool TrySendEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!IsReady)
            return false;

        try
        {
            if (parameters == null || parameters.Count == 0)
                MirraSDK.Analytics.Report(eventName);
            else
                MirraSDK.Analytics.Report(eventName, parameters);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Mirra analytics rejected '{eventName}': {exception.GetType().Name}");
            return false;
        }
    }

    public bool TryGameIsReady()
    {
        if (!IsReady)
            return false;
        try
        {
            MirraSDK.Analytics.GameIsReady();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Mirra native GameIsReady failed: {exception.GetType().Name}");
            return false;
        }
    }

    public bool TryGameplayStart()
    {
        if (!IsReady)
            return false;
        try
        {
            MirraSDK.Analytics.GameplayStart();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Mirra native GameplayStart failed: {exception.GetType().Name}");
            return false;
        }
    }

    public bool TryGameplayStop()
    {
        if (!IsReady)
            return false;
        try
        {
            MirraSDK.Analytics.GameplayStop();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Mirra native GameplayStop failed: {exception.GetType().Name}");
            return false;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

public abstract class AnalyticsProvider : MonoBehaviour
{
    public virtual bool IsReady => true;

    public abstract void Initialize();
    public abstract void SendEvent(string eventName, Dictionary<string, object> parameters = null);
    public abstract void SendEvent(string eventName, Dictionary<string, string> parameters = null);
    public abstract void SendEvent(string eventName);

    public virtual bool TrySendEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        SendEvent(eventName, parameters);
        return true;
    }
}

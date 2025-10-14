#if YG_SDK_ENABLED
using System.Collections.Generic;
using YG;
public class YG_MetricaProvider : AnalyticsProvider
{
    public override void Initialize()
    {
    }
    public override void SendEvent(string eventName, Dictionary<string, object> parameters)
    {
        YG2.MetricaSend(eventName, parameters);
    }
    public override void SendEvent(string eventName, Dictionary<string, string> parameters)
    {
        YG2.MetricaSend(eventName, parameters);
    }
    public override void SendEvent(string eventName)
    {
        YG2.MetricaSend(eventName);
    }
}
#endif
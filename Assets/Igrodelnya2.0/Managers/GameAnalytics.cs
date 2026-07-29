using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public static class GameAnalytics
{
    private const string HashSalt = "steal-brainrot-analytics-v1:";

    public static Dictionary<string, object> Params(params object[] keyValues)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (keyValues == null)
            return result;

        for (int i = 0; i + 1 < keyValues.Length; i += 2)
        {
            string key = Convert.ToString(keyValues[i], CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = keyValues[i + 1];
        }

        return result;
    }

    public static void Track(
        string eventName,
        Dictionary<string, object> parameters = null,
        AnalyticsPriority priority = AnalyticsPriority.Normal,
        string deduplicationKey = null)
    {
        AnalyticsManager.Instance.Track(eventName, parameters, priority, deduplicationKey);
    }

    public static void TrackCritical(
        string eventName,
        Dictionary<string, object> parameters = null,
        string deduplicationKey = null)
    {
        Track(eventName, parameters, AnalyticsPriority.Critical, deduplicationKey);
    }

    public static bool TrackOnce(
        string profileMilestoneKey,
        string eventName,
        Dictionary<string, object> parameters = null)
    {
        return AnalyticsManager.Instance.TrackOnce(profileMilestoneKey, eventName, parameters);
    }

    public static string HashId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return string.Empty;

        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(HashSalt + rawId.Trim().ToLowerInvariant());
            byte[] hash = sha.ComputeHash(bytes);
            var result = new StringBuilder(24);
            for (int i = 0; i < 12; i++)
                result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }

    public static IDisposable BeginItemGrant(
        string source,
        string sourceId = null,
        string currencyType = null,
        double price = 0d,
        string requestId = null)
    {
        return AnalyticsContext.PushItemGrant(new AnalyticsItemGrantContext
        {
            Source = source ?? string.Empty,
            SourceId = sourceId ?? string.Empty,
            CurrencyType = currencyType ?? string.Empty,
            Price = price,
            RequestId = requestId ?? string.Empty
        });
    }

    public static IDisposable BeginIncomeBatch(string collectionMode)
    {
        return AnalyticsContext.PushIncomeBatch(collectionMode);
    }
}

public sealed class AnalyticsItemGrantContext
{
    public string Source;
    public string SourceId;
    public string CurrencyType;
    public double Price;
    public string RequestId;
}

public static class AnalyticsContext
{
    private static AnalyticsItemGrantContext _itemGrant;
    private static int _incomeBatchDepth;
    private static string _incomeBatchMode = string.Empty;

    public static AnalyticsItemGrantContext ItemGrant => _itemGrant;
    public static bool IsIncomeBatch => _incomeBatchDepth > 0;
    public static string IncomeBatchMode => _incomeBatchMode;

    public static IDisposable PushItemGrant(AnalyticsItemGrantContext context)
    {
        AnalyticsItemGrantContext previous = _itemGrant;
        _itemGrant = context;
        return new Scope(() => _itemGrant = previous);
    }

    public static IDisposable PushIncomeBatch(string mode)
    {
        string previousMode = _incomeBatchMode;
        _incomeBatchDepth++;
        _incomeBatchMode = mode ?? string.Empty;
        return new Scope(() =>
        {
            _incomeBatchDepth = Math.Max(0, _incomeBatchDepth - 1);
            _incomeBatchMode = _incomeBatchDepth > 0 ? previousMode : string.Empty;
        });
    }

    private sealed class Scope : IDisposable
    {
        private Action _dispose;

        public Scope(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            Action dispose = _dispose;
            _dispose = null;
            dispose?.Invoke();
        }
    }
}

using System;

public static class OfflineRewardRules
{
    public const long MinimumAwaySeconds = 20L * 60L;
    public const long MaxAccrualSeconds = 8L * 60L * 60L;
    public const int AdMultiplier = 5;
    public const int BoostMultiplier = 10;
    public const int BoostPriceGems = 15;
    public const int MonthlyBoostMultiplier = 20;
    public const int MonthlyBoostDurationDays = 30;
    public const int MonthlyBoostPriceGems = 499;

    public static long ClampAccrualSeconds(long elapsedSeconds)
    {
        return Math.Max(0L, Math.Min(MaxAccrualSeconds, elapsedSeconds));
    }

    public static bool ShouldPresent(long elapsedSeconds, double baseReward)
    {
        return elapsedSeconds >= MinimumAwaySeconds && baseReward > 0d;
    }

    public static string FormatDuration(long seconds)
    {
        seconds = Math.Max(0L, seconds);
        long hours = seconds / 3600L;
        long minutes = seconds % 3600L / 60L;
        return hours > 0L
            ? LocalizationUtils.Format(
                "UI/Duration/HoursMinutesShort",
                "{0}h {1:00}m",
                hours,
                minutes)
            : LocalizationUtils.Format("UI/Duration/MinutesShort", "{0}m", minutes);
    }
}

public readonly struct OfflineRewardSnapshot
{
    public OfflineRewardSnapshot(
        long elapsedSeconds,
        double baseIncome,
        double displayedReward,
        int sourceCount)
    {
        ElapsedSeconds = Math.Max(0L, elapsedSeconds);
        CappedElapsedSeconds = OfflineRewardRules.ClampAccrualSeconds(elapsedSeconds);
        BaseIncome = Math.Max(0d, baseIncome);
        DisplayedReward = Math.Max(0d, displayedReward);
        SourceCount = Math.Max(0, sourceCount);
    }

    public long ElapsedSeconds { get; }
    public long CappedElapsedSeconds { get; }
    public double BaseIncome { get; }
    public double DisplayedReward { get; }
    public int SourceCount { get; }
    public double BoostedReward => DisplayedReward * OfflineRewardRules.BoostMultiplier;
    public double GetReward(int multiplier) =>
        DisplayedReward * Math.Max(1, multiplier);
}

using System.Text.Json.Serialization;

namespace AgyUsageTray.Models;

public class AgyUsageRoot
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public string ResponseText { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public AgyCommand? Command { get; set; }
}

public class AgyCommand
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public AgyUsageData? Data { get; set; }
}

public class AgyUsageData
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("groups")]
    public List<QuotaGroup> Groups { get; set; } = new();
}

public class QuotaGroup
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("buckets")]
    public List<QuotaBucket> Buckets { get; set; } = new();
}

public class QuotaBucket
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("window")]
    public string Window { get; set; } = string.Empty;

    [JsonPropertyName("remaining_fraction")]
    public double RemainingFraction { get; set; } = 1.0;

    [JsonPropertyName("reset_time")]
    public string ResetTimeString { get; set; } = string.Empty;

    [JsonIgnore]
    public double RemainingPercentage => Math.Clamp(RemainingFraction * 100.0, 0.0, 100.0);

    [JsonIgnore]
    public DateTimeOffset? ResetTime
    {
        get
        {
            if (DateTimeOffset.TryParse(ResetTimeString, out var dt))
                return dt;
            return null;
        }
    }

    public string GetFormattedResetCountdown()
    {
        if (ResetTime == null) return string.Empty;
        var diff = ResetTime.Value - DateTimeOffset.UtcNow;
        if (diff.TotalSeconds <= 0) return "Resetting now";

        if (diff.TotalDays >= 1)
        {
            int days = (int)diff.TotalDays;
            int hours = diff.Hours;
            return hours > 0 ? $"Resets in {days}d {hours}h" : $"Resets in {days}d";
        }
        if (diff.TotalHours >= 1)
        {
            int hours = (int)diff.TotalHours;
            int mins = diff.Minutes;
            return mins > 0 ? $"Resets in {hours}h {mins}m" : $"Resets in {hours}h";
        }
        return $"Resets in {Math.Max(1, (int)diff.TotalMinutes)}m";
    }
}

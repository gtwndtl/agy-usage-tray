using System.Text.Json;
using AgyUsageTray.Models;
using AgyUsageTray.Services;

namespace AgyUsageTray.Tests;

public class QuotaDataTests
{
    private const string SampleJson = """
    {
      "status": "SUCCESS",
      "command": {
        "name": "usage",
        "data": {
          "description": "Test description",
          "groups": [
            {
              "name": "Gemini Models",
              "description": "Models within this group: Gemini Flash, Gemini Pro",
              "buckets": [
                {
                  "id": "gemini-weekly",
                  "name": "Weekly Limit Remaining",
                  "window": "weekly",
                  "remaining_fraction": 0.971,
                  "reset_time": "2026-10-26T01:36:49Z"
                },
                {
                  "id": "gemini-5h",
                  "name": "Five Hour Limit Remaining",
                  "window": "5h",
                  "remaining_fraction": 0.913,
                  "reset_time": "2026-10-19T06:36:49Z"
                }
              ]
            },
            {
              "name": "Claude and GPT models",
              "buckets": [
                {
                  "id": "3p-weekly",
                  "name": "Weekly Limit Remaining",
                  "window": "weekly",
                  "remaining_fraction": 0.528,
                  "reset_time": "2026-10-21T07:34:42Z"
                }
              ]
            }
          ]
        }
      }
    }
    """;

    [Fact]
    public void DeserializesJsonCorrectly()
    {
        var root = JsonSerializer.Deserialize<AgyUsageRoot>(SampleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(root);
        Assert.Equal("SUCCESS", root.Status);
        Assert.NotNull(root.Command?.Data?.Groups);
        Assert.Equal(2, root.Command.Data.Groups.Count);

        var geminiGroup = root.Command.Data.Groups[0];
        Assert.Equal("Gemini Models", geminiGroup.Name);
        Assert.Equal(2, geminiGroup.Buckets.Count);

        var weeklyBucket = geminiGroup.Buckets[0];
        Assert.Equal("Weekly Limit Remaining", weeklyBucket.Name);
        Assert.Equal(97.1, Math.Round(weeklyBucket.RemainingPercentage, 1));
        Assert.NotNull(weeklyBucket.ResetTime);
    }

    [Fact]
    public void ResetCountdownCalculatesProperly()
    {
        var future = DateTimeOffset.UtcNow.AddHours(3).AddMinutes(20);
        var bucket = new QuotaBucket
        {
            ResetTimeString = future.ToString("o")
        };

        var countdown = bucket.GetFormattedResetCountdown();
        Assert.StartsWith("Resets in 3h", countdown);
    }

    [Fact]
    public void FindsAgyExecutable()
    {
        var path = AgyCliService.FindAgyExecutable();
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.Contains("agy", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveFetchUsageSucceeds()
    {
        var service = new AgyCliService();
        var (data, error) = await service.FetchUsageAsync();
        Assert.Null(error);
        Assert.NotNull(data);
        Assert.NotNull(data.Command?.Data?.Groups);
        Assert.True(data.Command.Data.Groups.Count > 0);
    }
}

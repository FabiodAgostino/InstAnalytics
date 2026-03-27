using System.Text.Json.Serialization;

namespace InstAnalytics.Models;

public class RemovalSession
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("unfollowedCount")]
    public int UnfollowedCount { get; set; }

    [JsonPropertyName("excludedCount")]
    public int ExcludedCount { get; set; }
}

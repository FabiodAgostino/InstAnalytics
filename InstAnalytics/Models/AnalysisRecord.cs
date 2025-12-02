using System.Text.Json.Serialization;

namespace InstAnalytics.Models;

/// <summary>
/// Represents a single historical analysis record with followers and following data.
/// </summary>
public class AnalysisRecord
{
    /// <summary>
    /// Timestamp when the analysis was performed (Rome timezone).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Number of followers at the time of analysis.
    /// </summary>
    [JsonPropertyName("followersCount")]
    public int FollowersCount { get; set; }

    /// <summary>
    /// Number of following at the time of analysis.
    /// </summary>
    [JsonPropertyName("followingCount")]
    public int FollowingCount { get; set; }

    /// <summary>
    /// Filename of the cleaned followers data in RawData folder.
    /// </summary>
    [JsonPropertyName("followersFile")]
    public string FollowersFile { get; set; } = string.Empty;

    /// <summary>
    /// Filename of the cleaned following data in RawData folder.
    /// </summary>
    [JsonPropertyName("followingFile")]
    public string FollowingFile { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the original followers HTML file content.
    /// Used for duplicate detection.
    /// </summary>
    [JsonPropertyName("followersFileHash")]
    public string FollowersFileHash { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the original following HTML file content.
    /// Used for duplicate detection.
    /// </summary>
    [JsonPropertyName("followingFileHash")]
    public string FollowingFileHash { get; set; } = string.Empty;

    /// <summary>
    /// Last modified date of the followers file from ZIP entry.
    /// Stored as metadata, NOT used for duplicate detection.
    /// </summary>
    [JsonPropertyName("followersFileLastModified")]
    public DateTime FollowersFileLastModified { get; set; }

    /// <summary>
    /// Last modified date of the following file from ZIP entry.
    /// Stored as metadata, NOT used for duplicate detection.
    /// </summary>
    [JsonPropertyName("followingFileLastModified")]
    public DateTime FollowingFileLastModified { get; set; }

    /// <summary>
    /// Calculated ratio of followers to following (not stored in JSON)
    /// </summary>
    [JsonIgnore]
    public double Ratio => FollowingCount > 0 ? (double)FollowersCount / FollowingCount : 0;
}

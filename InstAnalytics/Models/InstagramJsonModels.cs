using System.Text.Json.Serialization;

namespace InstAnalytics.Models;

/// <summary>
/// Root model for Instagram JSON export - Following
/// Format: { "relationships_following": [...] }
/// </summary>
public class InstagramFollowingJson
{
    [JsonPropertyName("relationships_following")]
    public List<InstagramFollowingEntry>? RelationshipsFollowing { get; set; }
}

/// <summary>
/// Represents a single following entry
/// Format: { "title": "username", "string_list_data": [...] }
/// </summary>
public class InstagramFollowingEntry
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("string_list_data")]
    public List<InstagramStringListData>? StringListData { get; set; }

    [JsonPropertyName("media_list_data")]
    public List<object>? MediaListData { get; set; }
}

/// <summary>
/// Represents a single followers entry
/// Format: { "title": "", "string_list_data": [{ "value": "username", "timestamp": ... }], "media_list_data": [] }
/// </summary>
public class InstagramFollowersEntry
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("string_list_data")]
    public List<InstagramStringListData>? StringListData { get; set; }

    [JsonPropertyName("media_list_data")]
    public List<object>? MediaListData { get; set; }
}

/// <summary>
/// String list data - used in both followers and following
/// Followers: contains "value" (username) and "timestamp"
/// Following: contains "href" and "timestamp", username is in parent "title"
/// </summary>
public class InstagramStringListData
{
    [JsonPropertyName("href")]
    public string? Href { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; set; }
}

using System.IO;
using System.Text.Json;
using InstAnalytics.Models;

namespace InstAnalytics.Services;

/// <summary>
/// Parser for Instagram following JSON export files
/// </summary>
public class InstagramFollowingJsonParser : IInstagramHtmlParser
{
    public async Task<List<InstagramUser>> ParseAsync(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException($"JSON file not found: {jsonFilePath}");
        }

        var users = new List<InstagramUser>();
        var jsonContent = await File.ReadAllTextAsync(jsonFilePath);

        try
        {
            var jsonData = JsonSerializer.Deserialize<InstagramFollowingJson>(jsonContent);

            if (jsonData?.RelationshipsFollowing != null)
            {
                foreach (var entry in jsonData.RelationshipsFollowing)
                {
                    // In following JSON, username is in "title" field, not in "value"
                    if (!string.IsNullOrEmpty(entry.Title))
                    {
                        DateTime? followDate = null;

                        // Get timestamp from string_list_data if available
                        if (entry.StringListData != null && entry.StringListData.Count > 0)
                        {
                            var data = entry.StringListData[0];
                            if (data.Timestamp.HasValue)
                            {
                                followDate = DateTimeOffset.FromUnixTimeSeconds(data.Timestamp.Value).DateTime;
                            }
                        }

                        users.Add(new InstagramUser(entry.Title, followDate));
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid JSON format in following file: {ex.Message}", ex);
        }

        return users;
    }
}

using System.IO;
using System.Text.Json;
using InstAnalytics.Models;

namespace InstAnalytics.Services;

/// <summary>
/// Parser for Instagram followers JSON export files
/// </summary>
public class InstagramFollowersJsonParser : IInstagramHtmlParser
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
            // Instagram followers JSON is an array of entries, not an object with "relationships_followers"
            var jsonData = JsonSerializer.Deserialize<List<InstagramFollowersEntry>>(jsonContent);

            if (jsonData != null)
            {
                foreach (var entry in jsonData)
                {
                    if (entry.StringListData != null)
                    {
                        foreach (var data in entry.StringListData)
                        {
                            // In followers JSON, username is in "value" field
                            if (!string.IsNullOrEmpty(data.Value))
                            {
                                DateTime? followDate = null;

                                // Convert Unix timestamp to DateTime if available
                                if (data.Timestamp.HasValue)
                                {
                                    followDate = DateTimeOffset.FromUnixTimeSeconds(data.Timestamp.Value).DateTime;
                                }

                                users.Add(new InstagramUser(data.Value, followDate));
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid JSON format in followers file: {ex.Message}", ex);
        }

        return users;
    }
}

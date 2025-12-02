using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using InstAnalytics.Models;

namespace InstAnalytics.Services;

/// <summary>
/// Parser for Instagram following HTML export files
/// </summary>
public class InstagramFollowingParser : IInstagramHtmlParser
{
    // Pattern for following: <h2 class="_3-95 _2pim _a6-h _a6-i">USERNAME</h2>
    private static readonly Regex UsernamePattern = new(
        @"<h2\s+class=""[^""]*_a6-h[^""]*"">([^<]+)</h2>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // Pattern for date: <div>Nov 26, 2025 12:15 am</div>
    private static readonly Regex DatePattern = new(
        @"<div>([^<]+\d{4}\s+\d{1,2}:\d{2}\s+(?:am|pm))</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public async Task<List<InstagramUser>> ParseAsync(string htmlFilePath)
    {
        if (!File.Exists(htmlFilePath))
        {
            throw new FileNotFoundException($"HTML file not found: {htmlFilePath}");
        }

        var users = new List<InstagramUser>();
        var htmlContent = await File.ReadAllTextAsync(htmlFilePath);

        var usernameMatches = UsernamePattern.Matches(htmlContent);
        var dateMatches = DatePattern.Matches(htmlContent);

        for (int i = 0; i < usernameMatches.Count; i++)
        {
            var username = usernameMatches[i].Groups[1].Value;
            DateTime? followDate = null;

            // Try to match the corresponding date
            if (i < dateMatches.Count)
            {
                var dateString = dateMatches[i].Groups[1].Value;
                if (TryParseInstagramDate(dateString, out var parsedDate))
                {
                    followDate = parsedDate;
                }
            }

            users.Add(new InstagramUser(username, followDate));
        }

        return users;
    }

    private static bool TryParseInstagramDate(string dateString, out DateTime result)
    {
        // Instagram date format: "Nov 27, 2025 1:12 am"
        var formats = new[]
        {
            "MMM dd, yyyy h:mm tt",
            "MMM d, yyyy h:mm tt"
        };

        return DateTime.TryParseExact(
            dateString,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result
        );
    }
}

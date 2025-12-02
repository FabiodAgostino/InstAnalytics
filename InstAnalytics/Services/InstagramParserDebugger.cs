using System.IO;
using System.Text.RegularExpressions;

namespace InstAnalytics.Services;

/// <summary>
/// Debug utility to analyze HTML parsing issues
/// </summary>
public class InstagramParserDebugger
{
    public static async Task<string> AnalyzeHtmlStructureAsync(string htmlFilePath)
    {
        if (!File.Exists(htmlFilePath))
        {
            return "File not found";
        }

        var htmlContent = await File.ReadAllTextAsync(htmlFilePath);
        var report = new System.Text.StringBuilder();

        report.AppendLine($"File: {Path.GetFileName(htmlFilePath)}");
        report.AppendLine($"Size: {htmlContent.Length:N0} characters");
        report.AppendLine();

        // Test followers pattern
        var followersPattern = new Regex(
            @"<a\s+target=""_blank""\s+href=""https://www\.instagram\.com/([^""]+)"">",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        var followersMatches = followersPattern.Matches(htmlContent);
        report.AppendLine($"Followers pattern matches: {followersMatches.Count}");
        if (followersMatches.Count > 0)
        {
            report.AppendLine($"  First match: {followersMatches[0].Groups[1].Value}");
            if (followersMatches.Count > 1)
                report.AppendLine($"  Last match: {followersMatches[followersMatches.Count - 1].Groups[1].Value}");
        }
        report.AppendLine();

        // Test following pattern
        var followingPattern = new Regex(
            @"<h2\s+class=""[^""]*_a6-h[^""]*"">([^<]+)</h2>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        var followingMatches = followingPattern.Matches(htmlContent);
        report.AppendLine($"Following pattern matches: {followingMatches.Count}");
        if (followingMatches.Count > 0)
        {
            report.AppendLine($"  First match: {followingMatches[0].Groups[1].Value}");
            if (followingMatches.Count > 1)
                report.AppendLine($"  Last match: {followingMatches[followingMatches.Count - 1].Groups[1].Value}");
        }
        report.AppendLine();

        // Look for alternative patterns
        var linkPattern = new Regex(@"instagram\.com/([a-zA-Z0-9._]+)", RegexOptions.Compiled);
        var linkMatches = linkPattern.Matches(htmlContent);
        report.AppendLine($"Total Instagram links found: {linkMatches.Count}");
        report.AppendLine();

        // Sample first 500 characters
        report.AppendLine("First 500 characters:");
        report.AppendLine(htmlContent.Substring(0, Math.Min(500, htmlContent.Length)));

        return report.ToString();
    }
}

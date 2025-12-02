using InstAnalytics.Models;

namespace InstAnalytics.Services;

public interface IInstagramHtmlParser
{
    /// <summary>
    /// Extracts Instagram usernames from an HTML file
    /// </summary>
    /// <param name="htmlFilePath">Path to the HTML file</param>
    /// <returns>List of Instagram users extracted from the file</returns>
    Task<List<InstagramUser>> ParseAsync(string htmlFilePath);
}

using InstAnalytics.Models;

namespace InstAnalytics.Services;

/// <summary>
/// Service to analyze Instagram followers and following data
/// </summary>
public class InstagramAnalyzerService
{
    private readonly IInstagramHtmlParser _followersParser;
    private readonly IInstagramHtmlParser _followingParser;

    public InstagramAnalyzerService()
    {
        _followersParser = new InstagramFollowersParser();
        _followingParser = new InstagramFollowingParser();
    }

    /// <summary>
    /// Gets all followers from HTML file
    /// </summary>
    public async Task<List<InstagramUser>> GetFollowersAsync(string followersFilePath)
    {
        return await _followersParser.ParseAsync(followersFilePath);
    }

    /// <summary>
    /// Gets all following from HTML file
    /// </summary>
    public async Task<List<InstagramUser>> GetFollowingAsync(string followingFilePath)
    {
        return await _followingParser.ParseAsync(followingFilePath);
    }

    /// <summary>
    /// Gets users that you follow but don't follow you back
    /// </summary>
    public async Task<List<InstagramUser>> GetNotFollowingBackAsync(string followersFilePath, string followingFilePath)
    {
        var followers = await GetFollowersAsync(followersFilePath);
        var following = await GetFollowingAsync(followingFilePath);

        var followerUsernames = followers.Select(f => f.Username.ToLowerInvariant()).ToHashSet();

        return following
            .Where(f => !followerUsernames.Contains(f.Username.ToLowerInvariant()))
            .ToList();
    }

    /// <summary>
    /// Gets users that follow you but you don't follow back
    /// </summary>
    public async Task<List<InstagramUser>> GetNotFollowingAsync(string followersFilePath, string followingFilePath)
    {
        var followers = await GetFollowersAsync(followersFilePath);
        var following = await GetFollowingAsync(followingFilePath);

        var followingUsernames = following.Select(f => f.Username.ToLowerInvariant()).ToHashSet();

        return followers
            .Where(f => !followingUsernames.Contains(f.Username.ToLowerInvariant()))
            .ToList();
    }

    /// <summary>
    /// Gets mutual followers (users that follow you and you follow back)
    /// </summary>
    public async Task<List<InstagramUser>> GetMutualFollowersAsync(string followersFilePath, string followingFilePath)
    {
        var followers = await GetFollowersAsync(followersFilePath);
        var following = await GetFollowingAsync(followingFilePath);

        var followingUsernames = following.Select(f => f.Username.ToLowerInvariant()).ToHashSet();

        return followers
            .Where(f => followingUsernames.Contains(f.Username.ToLowerInvariant()))
            .ToList();
    }
}

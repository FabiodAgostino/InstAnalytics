using System.IO;

namespace InstAnalytics.Services;

/// <summary>
/// Test utility to verify parsers work correctly
/// </summary>
public static class InstagramParserTester
{
    public static async Task TestParsersAsync()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = Directory.GetParent(baseDirectory)?.Parent?.Parent?.Parent?.Parent?.FullName;

        if (projectRoot == null)
        {
            Console.WriteLine("Could not find project root directory");
            return;
        }

        var followersFile = Path.Combine(projectRoot, "followers_1.html");
        var followingFile = Path.Combine(projectRoot, "following.html");

        Console.WriteLine($"Testing parsers...");
        Console.WriteLine($"Followers file: {followersFile}");
        Console.WriteLine($"Following file: {followingFile}");
        Console.WriteLine();

        if (!File.Exists(followersFile))
        {
            Console.WriteLine($"Followers file not found: {followersFile}");
            return;
        }

        if (!File.Exists(followingFile))
        {
            Console.WriteLine($"Following file not found: {followingFile}");
            return;
        }

        var analyzer = new InstagramAnalyzerService();

        // Test followers parser
        Console.WriteLine("=== FOLLOWERS ===");
        var followers = await analyzer.GetFollowersAsync(followersFile);
        Console.WriteLine($"Total followers: {followers.Count}");
        Console.WriteLine($"First 5 followers:");
        foreach (var follower in followers.Take(5))
        {
            Console.WriteLine($"  - {follower.Username} (followed on: {follower.FollowDate?.ToString("yyyy-MM-dd HH:mm") ?? "unknown"})");
        }
        Console.WriteLine();

        // Test following parser
        Console.WriteLine("=== FOLLOWING ===");
        var following = await analyzer.GetFollowingAsync(followingFile);
        Console.WriteLine($"Total following: {following.Count}");
        Console.WriteLine($"First 5 following:");
        foreach (var user in following.Take(5))
        {
            Console.WriteLine($"  - {user.Username} (followed on: {user.FollowDate?.ToString("yyyy-MM-dd HH:mm") ?? "unknown"})");
        }
        Console.WriteLine();

        // Test analysis
        Console.WriteLine("=== ANALYSIS ===");
        var notFollowingBack = await analyzer.GetNotFollowingBackAsync(followersFile, followingFile);
        Console.WriteLine($"Not following back: {notFollowingBack.Count}");
        Console.WriteLine($"First 5:");
        foreach (var user in notFollowingBack.Take(5))
        {
            Console.WriteLine($"  - {user.Username}");
        }
        Console.WriteLine();

        var notFollowing = await analyzer.GetNotFollowingAsync(followersFile, followingFile);
        Console.WriteLine($"Not following: {notFollowing.Count}");
        Console.WriteLine($"First 5:");
        foreach (var user in notFollowing.Take(5))
        {
            Console.WriteLine($"  - {user.Username}");
        }
        Console.WriteLine();

        var mutualFollowers = await analyzer.GetMutualFollowersAsync(followersFile, followingFile);
        Console.WriteLine($"Mutual followers: {mutualFollowers.Count}");
        Console.WriteLine($"First 5:");
        foreach (var user in mutualFollowers.Take(5))
        {
            Console.WriteLine($"  - {user.Username}");
        }
    }
}

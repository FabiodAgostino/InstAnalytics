using System.IO;
using System.Text;
using System.Text.Json;
using InstAnalytics.Models;

namespace InstAnalytics.Services;

/// <summary>
/// Service for managing historical analysis data.
/// Handles reading/writing statistics.json and managing raw data files.
/// </summary>
public class HistoricalDataService
{
    private readonly string _historicalDataPath;
    private readonly string _rawDataPath;
    private readonly string _statisticsFilePath;
    private readonly TimeZoneInfo _romeTimeZone;

    // JSON serialization options
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Container for statistics data.
    /// </summary>
    private class StatisticsData
    {
        public List<AnalysisRecord> Analyses { get; set; } = new();
    }

    public HistoricalDataService(string? basePath = null)
    {
        // Use provided path or default to HistoricalData in project directory
        _historicalDataPath = basePath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "HistoricalData");

        _rawDataPath = Path.Combine(_historicalDataPath, "RawData");
        _statisticsFilePath = Path.Combine(_historicalDataPath, "statistics.json");

        // Initialize Rome timezone
        _romeTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");

        // Ensure directories exist
        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Gets the current DateTime in Rome timezone.
    /// </summary>
    public DateTime GetRomeTimestamp()
    {
        return TimeZoneInfo.ConvertTime(DateTime.Now, _romeTimeZone);
    }

    /// <summary>
    /// Ensures that the HistoricalData and RawData directories exist.
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(_historicalDataPath))
        {
            Directory.CreateDirectory(_historicalDataPath);
        }

        if (!Directory.Exists(_rawDataPath))
        {
            Directory.CreateDirectory(_rawDataPath);
        }
    }

    /// <summary>
    /// Saves a new analysis to the historical data.
    /// </summary>
    /// <param name="followers">List of follower usernames.</param>
    /// <param name="following">List of following usernames.</param>
    /// <param name="followersHash">SHA256 hash of followers HTML file.</param>
    /// <param name="followingHash">SHA256 hash of following HTML file.</param>
    /// <param name="followersLastModified">Last modified date of followers file from ZIP.</param>
    /// <param name="followingLastModified">Last modified date of following file from ZIP.</param>
    public async Task SaveAnalysisAsync(
        List<string> followers,
        List<string> following,
        string followersHash,
        string followingHash,
        DateTime followersLastModified,
        DateTime followingLastModified)
    {
        var timestamp = GetRomeTimestamp();
        var timestampString = timestamp.ToString("yyyyMMddHHmmss");

        // Create filenames
        var followersFileName = $"followers_{timestampString}.txt";
        var followingFileName = $"following_{timestampString}.txt";

        // Save raw data files
        await SaveRawDataFileAsync(followersFileName, followers);
        await SaveRawDataFileAsync(followingFileName, following);

        // Create analysis record
        var record = new AnalysisRecord
        {
            Timestamp = timestamp,
            FollowersCount = followers.Count,
            FollowingCount = following.Count,
            FollowersFile = followersFileName,
            FollowingFile = followingFileName,
            FollowersFileHash = followersHash,
            FollowingFileHash = followingHash,
            FollowersFileLastModified = followersLastModified,
            FollowingFileLastModified = followingLastModified
        };

        // Load existing statistics
        var statistics = await LoadStatisticsDataAsync();

        // Add new record
        statistics.Analyses.Add(record);

        // Save updated statistics
        await SaveStatisticsDataAsync(statistics);
    }

    /// <summary>
    /// Saves a list of usernames to a raw data file.
    /// </summary>
    private async Task SaveRawDataFileAsync(string fileName, List<string> usernames)
    {
        var filePath = Path.Combine(_rawDataPath, fileName);
        await File.WriteAllLinesAsync(filePath, usernames, Encoding.UTF8);
    }

    /// <summary>
    /// Loads all historical statistics.
    /// </summary>
    /// <returns>List of analysis records sorted by timestamp (newest first).</returns>
    public async Task<List<AnalysisRecord>> LoadStatisticsAsync()
    {
        var statistics = await LoadStatisticsDataAsync();
        return statistics.Analyses
            .OrderByDescending(a => a.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Loads the statistics data from JSON file.
    /// </summary>
    private async Task<StatisticsData> LoadStatisticsDataAsync()
    {
        if (!File.Exists(_statisticsFilePath))
        {
            return new StatisticsData();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_statisticsFilePath, Encoding.UTF8);
            var data = JsonSerializer.Deserialize<StatisticsData>(json, JsonOptions);
            return data ?? new StatisticsData();
        }
        catch (JsonException)
        {
            // If JSON is corrupted, return empty data
            return new StatisticsData();
        }
    }

    /// <summary>
    /// Saves the statistics data to JSON file.
    /// </summary>
    private async Task SaveStatisticsDataAsync(StatisticsData statistics)
    {
        var json = JsonSerializer.Serialize(statistics, JsonOptions);
        await File.WriteAllTextAsync(_statisticsFilePath, json, Encoding.UTF8);
    }

    /// <summary>
    /// Checks if an analysis with the given file hashes already exists.
    /// </summary>
    /// <param name="followersHash">SHA256 hash of followers HTML file.</param>
    /// <param name="followingHash">SHA256 hash of following HTML file.</param>
    /// <returns>True if a duplicate exists, false otherwise.</returns>
    public async Task<bool> IsDuplicateAnalysisAsync(string followersHash, string followingHash)
    {
        var statistics = await LoadStatisticsDataAsync();
        return statistics.Analyses.Any(a =>
            a.FollowersFileHash == followersHash &&
            a.FollowingFileHash == followingHash);
    }

    /// <summary>
    /// Gets an existing analysis by file hashes.
    /// </summary>
    /// <param name="followersHash">SHA256 hash of followers HTML file.</param>
    /// <param name="followingHash">SHA256 hash of following HTML file.</param>
    /// <returns>Analysis record if found, null otherwise.</returns>
    public async Task<AnalysisRecord?> GetAnalysisByHashAsync(string followersHash, string followingHash)
    {
        var statistics = await LoadStatisticsDataAsync();
        return statistics.Analyses.FirstOrDefault(a =>
            a.FollowersFileHash == followersHash &&
            a.FollowingFileHash == followingHash);
    }

    /// <summary>
    /// Deletes a specific analysis and its associated raw data files.
    /// </summary>
    /// <param name="timestamp">Timestamp of the analysis to delete.</param>
    public async Task DeleteAnalysisAsync(DateTime timestamp)
    {
        var statistics = await LoadStatisticsDataAsync();

        // Find the analysis to delete
        var analysis = statistics.Analyses.FirstOrDefault(a => a.Timestamp == timestamp);
        if (analysis == null)
        {
            return;
        }

        // Delete raw data files
        DeleteRawDataFile(analysis.FollowersFile);
        DeleteRawDataFile(analysis.FollowingFile);

        // Remove from list
        statistics.Analyses.Remove(analysis);

        // Save updated statistics
        await SaveStatisticsDataAsync(statistics);
    }

    /// <summary>
    /// Deletes a raw data file if it exists.
    /// </summary>
    private void DeleteRawDataFile(string fileName)
    {
        var filePath = Path.Combine(_rawDataPath, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Deletes multiple analyses and their associated raw data files.
    /// </summary>
    /// <param name="timestamps">List of timestamps to delete.</param>
    public async Task DeleteMultipleAnalysesAsync(List<DateTime> timestamps)
    {
        foreach (var timestamp in timestamps)
        {
            await DeleteAnalysisAsync(timestamp);
        }
    }

    /// <summary>
    /// Loads raw data for a specific analysis.
    /// </summary>
    /// <param name="record">Analysis record.</param>
    /// <returns>Tuple with followers and following username lists.</returns>
    public async Task<(List<string> Followers, List<string> Following)> LoadRawDataAsync(AnalysisRecord record)
    {
        var followersPath = Path.Combine(_rawDataPath, record.FollowersFile);
        var followingPath = Path.Combine(_rawDataPath, record.FollowingFile);

        var followers = File.Exists(followersPath)
            ? (await File.ReadAllLinesAsync(followersPath, Encoding.UTF8)).ToList()
            : new List<string>();

        var following = File.Exists(followingPath)
            ? (await File.ReadAllLinesAsync(followingPath, Encoding.UTF8)).ToList()
            : new List<string>();

        return (followers, following);
    }

    /// <summary>
    /// Gets the total number of analyses stored.
    /// </summary>
    public async Task<int> GetAnalysisCountAsync()
    {
        var statistics = await LoadStatisticsDataAsync();
        return statistics.Analyses.Count;
    }

    /// <summary>
    /// Gets the date range of stored analyses.
    /// </summary>
    /// <returns>Tuple with oldest and newest dates, or null if no analyses exist.</returns>
    public async Task<(DateTime? Oldest, DateTime? Newest)?> GetDateRangeAsync()
    {
        var statistics = await LoadStatisticsDataAsync();
        if (!statistics.Analyses.Any())
        {
            return null;
        }

        var oldest = statistics.Analyses.Min(a => a.Timestamp);
        var newest = statistics.Analyses.Max(a => a.Timestamp);

        return (oldest, newest);
    }
}

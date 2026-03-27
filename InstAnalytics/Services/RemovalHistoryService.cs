using System.IO;
using System.Text.Json;
using InstAnalytics.Models;

namespace InstAnalytics.Services;

public class RemovalHistoryService
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RemovalHistoryService(string? basePath = null)
    {
        var dir = basePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HistoricalData");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "removal_sessions.json");
    }

    public async Task<List<RemovalSession>> LoadSessionsAsync()
    {
        if (!File.Exists(_filePath)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<RemovalSession>>(json, JsonOptions) ?? [];
        }
        catch { return []; }
    }

    public async Task SaveSessionAsync(RemovalSession session)
    {
        var sessions = await LoadSessionsAsync();
        sessions.Add(session);
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(sessions, JsonOptions));
    }
}

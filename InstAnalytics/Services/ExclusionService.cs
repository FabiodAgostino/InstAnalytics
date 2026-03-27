using System.IO;
using System.Text.Json;

namespace InstAnalytics.Services;

/// <summary>
/// Persists a set of excluded usernames to disk.
/// Excluded users are filtered out of analysis results.
/// </summary>
public class ExclusionService
{
    private readonly string _exclusionFilePath;
    private HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);

    public ExclusionService()
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HistoricalData");
        Directory.CreateDirectory(dataDir);
        _exclusionFilePath = Path.Combine(dataDir, "excluded.json");
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_exclusionFilePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(_exclusionFilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            _excluded = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        }
        catch { }
    }

    public async Task AddAsync(string username)
    {
        _excluded.Add(username);
        await SaveAsync();
    }

    public async Task RemoveAsync(string username)
    {
        _excluded.Remove(username);
        await SaveAsync();
    }

    public bool IsExcluded(string username) => _excluded.Contains(username);

    public IReadOnlyList<string> GetAll() =>
        _excluded.OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(
            _excluded.OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList(),
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_exclusionFilePath, json);
    }
}

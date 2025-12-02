using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace InstAnalytics.Services;

/// <summary>
/// Service for handling Instagram ZIP export files.
/// Extracts HTML files in-memory and calculates file hashes for duplicate detection.
/// </summary>
public class InstagramZipService : IDisposable
{
    private ZipArchive? _zipArchive;
    private FileStream? _fileStream;

    // Path constants for Instagram export structure - HTML format
    private const string FollowersHtmlPattern = "connections/followers_and_following/followers_";
    private const string FollowingHtmlPath = "connections/followers_and_following/following.html";

    // Path constants for Instagram export structure - JSON format
    // Instagram uses followers_1.json, followers_2.json, etc. for followers
    private const string FollowersJsonPattern = "connections/followers_and_following/followers_";
    private const string FollowingJsonPath = "connections/followers_and_following/following.json";

    // Alternative JSON paths (Instagram may use different structures)
    private const string AltFollowersJsonPattern = "followers_and_following/followers_";
    private const string AltFollowingJsonPath = "followers_and_following/following.json";

    /// <summary>
    /// Opens an Instagram ZIP export file.
    /// </summary>
    /// <param name="zipPath">Full path to the ZIP file.</param>
    /// <exception cref="FileNotFoundException">If the ZIP file doesn't exist.</exception>
    /// <exception cref="InvalidDataException">If the file is not a valid ZIP archive.</exception>
    public async Task OpenZipAsync(string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException($"ZIP file not found: {zipPath}");
        }

        try
        {
            // Open file stream with read access
            _fileStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _zipArchive = new ZipArchive(_fileStream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException)
        {
            _fileStream?.Dispose();
            throw new InvalidDataException("The file is not a valid ZIP archive.");
        }
        catch
        {
            _fileStream?.Dispose();
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Extracts all followers HTML files and combines them into a single HTML content.
    /// Instagram may split followers across multiple files (followers_1.html, followers_2.html, etc.).
    /// </summary>
    /// <returns>Combined HTML content as string.</returns>
    /// <exception cref="InvalidOperationException">If ZIP is not opened or no followers files found.</exception>
    public async Task<string> ExtractFollowersHtmlAsync()
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        // Find all followers files (followers_1.html, followers_2.html, etc.)
        var followersEntries = _zipArchive.Entries
            .Where(e => e.FullName.StartsWith(FollowersHtmlPattern, StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName)
            .ToList();

        if (followersEntries.Count == 0)
        {
            throw new InvalidOperationException(
                $"No followers HTML files found in ZIP. Expected files starting with: {FollowersHtmlPattern}");
        }

        var combinedHtml = new StringBuilder();
        var isFirst = true;

        foreach (var entry in followersEntries)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();

            if (isFirst)
            {
                // First file: keep everything
                combinedHtml.Append(content);
                isFirst = false;
            }
            else
            {
                // Subsequent files: extract only the body content (between <body> and </body>)
                var bodyStartIndex = content.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
                var bodyEndIndex = content.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);

                if (bodyStartIndex >= 0 && bodyEndIndex > bodyStartIndex)
                {
                    // Find the end of the opening <body> tag
                    var bodyOpenEndIndex = content.IndexOf(">", bodyStartIndex);
                    if (bodyOpenEndIndex > 0)
                    {
                        var bodyContent = content.Substring(bodyOpenEndIndex + 1, bodyEndIndex - bodyOpenEndIndex - 1);

                        // Insert before the closing </body> tag of the combined HTML
                        var combinedBodyEndIndex = combinedHtml.ToString().LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                        if (combinedBodyEndIndex > 0)
                        {
                            combinedHtml.Insert(combinedBodyEndIndex, bodyContent);
                        }
                    }
                }
            }
        }

        return combinedHtml.ToString();
    }

    /// <summary>
    /// Extracts the following HTML file content to memory.
    /// </summary>
    /// <returns>HTML content as string.</returns>
    /// <exception cref="InvalidOperationException">If ZIP is not opened or file not found.</exception>
    public async Task<string> ExtractFollowingHtmlAsync()
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        var entry = _zipArchive.GetEntry(FollowingHtmlPath);
        if (entry == null)
        {
            throw new InvalidOperationException(
                $"Following HTML file not found in ZIP. Expected path: {FollowingHtmlPath}");
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Calculates SHA256 hash of file content.
    /// </summary>
    /// <param name="content">File content as string.</param>
    /// <returns>Hexadecimal hash string (lowercase).</returns>
    public static string CalculateFileHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);

        // Convert to hex string (lowercase)
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets metadata (size, last modified date) for a specific file in the ZIP.
    /// </summary>
    /// <param name="entryName">Entry path in ZIP (e.g., "connections/followers_and_following/followers_1.html").</param>
    /// <returns>Tuple with file size and last modified date.</returns>
    /// <exception cref="InvalidOperationException">If ZIP is not opened or entry not found.</exception>
    public (long Size, DateTime LastModified) GetFileMetadata(string entryName)
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        var entry = _zipArchive.GetEntry(entryName);
        if (entry == null)
        {
            throw new InvalidOperationException($"Entry not found in ZIP: {entryName}");
        }

        return (entry.Length, entry.LastWriteTime.DateTime);
    }

    /// <summary>
    /// Gets metadata for the first followers HTML file.
    /// </summary>
    public (long Size, DateTime LastModified) GetFollowersMetadata()
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        var firstFollowersEntry = _zipArchive.Entries
            .Where(e => e.FullName.StartsWith(FollowersHtmlPattern, StringComparison.OrdinalIgnoreCase)
                     && e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName)
            .FirstOrDefault();

        if (firstFollowersEntry == null)
        {
            throw new InvalidOperationException("No followers files found in ZIP.");
        }

        return (firstFollowersEntry.Length, firstFollowersEntry.LastWriteTime.DateTime);
    }

    /// <summary>
    /// Gets metadata for the following HTML file.
    /// </summary>
    public (long Size, DateTime LastModified) GetFollowingMetadata()
    {
        return GetFileMetadata(FollowingHtmlPath);
    }

    /// <summary>
    /// Validates that the ZIP contains the required Instagram export structure.
    /// Supports both HTML and JSON formats.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool ValidateZipStructure()
    {
        if (_zipArchive == null)
        {
            return false;
        }

        // Check for HTML format
        var hasFollowersHtml = _zipArchive.Entries.Any(e =>
            e.FullName.StartsWith(FollowersHtmlPattern, StringComparison.OrdinalIgnoreCase)
            && e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase));

        var hasFollowingHtml = _zipArchive.GetEntry(FollowingHtmlPath) != null;

        // Check for JSON format
        var hasFollowersJson = _zipArchive.Entries.Any(e =>
            (e.FullName.StartsWith(FollowersJsonPattern, StringComparison.OrdinalIgnoreCase) ||
             e.FullName.StartsWith(AltFollowersJsonPattern, StringComparison.OrdinalIgnoreCase))
            && e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        var hasFollowingJson = _zipArchive.GetEntry(FollowingJsonPath) != null ||
                               _zipArchive.GetEntry(AltFollowingJsonPath) != null;

        // Valid if either HTML or JSON format is present
        return (hasFollowersHtml && hasFollowingHtml) || (hasFollowersJson && hasFollowingJson);
    }

    /// <summary>
    /// Gets the number of followers files found in the ZIP.
    /// </summary>
    public int GetFollowersFileCount()
    {
        if (_zipArchive == null)
        {
            return 0;
        }

        return _zipArchive.Entries.Count(e =>
            e.FullName.StartsWith(FollowersHtmlPattern, StringComparison.OrdinalIgnoreCase)
            && e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Lists all files in the connections/followers_and_following directory.
    /// </summary>
    public List<string> ListFollowersAndFollowingFiles()
    {
        if (_zipArchive == null)
        {
            return new List<string>();
        }

        return _zipArchive.Entries
            .Where(e => e.FullName.Contains("followers_and_following/", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.FullName)
            .ToList();
    }

    /// <summary>
    /// Detects whether the ZIP contains JSON or HTML format data.
    /// </summary>
    /// <returns>True if JSON format is detected, false if HTML format.</returns>
    public bool IsJsonFormat()
    {
        if (_zipArchive == null)
        {
            return false;
        }

        // Check for JSON files (followers_1.json, followers_2.json, etc.)
        var hasFollowersJson = _zipArchive.Entries.Any(e =>
            (e.FullName.StartsWith(FollowersJsonPattern, StringComparison.OrdinalIgnoreCase) ||
             e.FullName.StartsWith(AltFollowersJsonPattern, StringComparison.OrdinalIgnoreCase))
            && e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        var hasFollowingJson = _zipArchive.GetEntry(FollowingJsonPath) != null ||
                               _zipArchive.GetEntry(AltFollowingJsonPath) != null;

        return hasFollowersJson || hasFollowingJson;
    }

    /// <summary>
    /// Extracts all followers JSON files and combines them into a single JSON array.
    /// Instagram may split followers across multiple files (followers_1.json, followers_2.json, etc.).
    /// </summary>
    /// <returns>Combined JSON array as string.</returns>
    /// <exception cref="InvalidOperationException">If ZIP is not opened or no followers files found.</exception>
    public async Task<string> ExtractFollowersJsonAsync()
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        // Find all followers JSON files
        var followersEntries = _zipArchive.Entries
            .Where(e => (e.FullName.StartsWith(FollowersJsonPattern, StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.StartsWith(AltFollowersJsonPattern, StringComparison.OrdinalIgnoreCase))
                     && e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName)
            .ToList();

        if (followersEntries.Count == 0)
        {
            throw new InvalidOperationException(
                $"No followers JSON files found in ZIP. Expected files starting with: {FollowersJsonPattern}");
        }

        // Parse and combine all followers arrays
        var allFollowers = new List<System.Text.Json.JsonElement>();

        foreach (var entry in followersEntries)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();

            // Parse as JSON array
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
            foreach (var element in jsonDoc.RootElement.EnumerateArray())
            {
                allFollowers.Add(element.Clone());
            }
        }

        // Serialize combined array back to JSON
        return System.Text.Json.JsonSerializer.Serialize(allFollowers);
    }

    /// <summary>
    /// Extracts the following JSON file content.
    /// </summary>
    /// <returns>JSON content as string.</returns>
    /// <exception cref="InvalidOperationException">If ZIP is not opened or file not found.</exception>
    public async Task<string> ExtractFollowingJsonAsync()
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        // Try primary path first, then alternative
        var entry = _zipArchive.GetEntry(FollowingJsonPath) ?? _zipArchive.GetEntry(AltFollowingJsonPath);

        if (entry == null)
        {
            throw new InvalidOperationException(
                $"Following JSON file not found in ZIP. Expected paths: {FollowingJsonPath} or {AltFollowingJsonPath}");
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Gets metadata for the first followers JSON file.
    /// </summary>
    public (long Size, DateTime LastModified) GetFollowersJsonMetadata()
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        var firstFollowersEntry = _zipArchive.Entries
            .Where(e => (e.FullName.StartsWith(FollowersJsonPattern, StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.StartsWith(AltFollowersJsonPattern, StringComparison.OrdinalIgnoreCase))
                     && e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName)
            .FirstOrDefault();

        if (firstFollowersEntry == null)
        {
            throw new InvalidOperationException("No followers JSON files found in ZIP.");
        }

        return (firstFollowersEntry.Length, firstFollowersEntry.LastWriteTime.DateTime);
    }

    /// <summary>
    /// Gets metadata for the following JSON file.
    /// </summary>
    public (long Size, DateTime LastModified) GetFollowingJsonMetadata()
    {
        if (_zipArchive == null)
        {
            throw new InvalidOperationException("ZIP archive not opened. Call OpenZipAsync first.");
        }

        var entry = _zipArchive.GetEntry(FollowingJsonPath) ?? _zipArchive.GetEntry(AltFollowingJsonPath);

        if (entry == null)
        {
            throw new InvalidOperationException("No following JSON file found in ZIP.");
        }

        return (entry.Length, entry.LastWriteTime.DateTime);
    }

    public void Dispose()
    {
        _zipArchive?.Dispose();
        _fileStream?.Dispose();
    }
}

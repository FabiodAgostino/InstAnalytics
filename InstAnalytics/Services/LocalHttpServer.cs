using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace InstAnalytics.Services;

/// <summary>
/// Minimal HTTP server on localhost that receives commands from the Chrome extension.
/// Supported endpoints:
///   GET  /status   → {"status":"running","version":"1.0"}
///   POST /exclude  → body {"username":"someuser"} → adds to exclusion list
/// CORS headers allow any chrome-extension:// origin.
/// </summary>
public sealed class LocalHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly ExclusionService _exclusionService;
    private CancellationTokenSource? _cts;

    public const int Port = 27182;
    public bool IsRunning { get; private set; }

    /// <summary>Fired on the thread pool when a user is excluded via the extension.</summary>
    public event Action<string>? UserExcluded;

    /// <summary>Fired on the thread pool when Auto-Clean reports a result for a user.
    /// Parameters: username, status ("unfollowed" | "excluded" | "already_removed")</summary>
    public event Action<string, string>? UnfollowResultReceived;

    public LocalHttpServer(ExclusionService exclusionService)
    {
        _exclusionService = exclusionService;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _ = ListenAsync(_cts.Token);
        }
        catch (HttpListenerException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LocalHttpServer] Cannot start on port {Port}: {ex.Message}");
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = HandleAsync(ctx);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
            catch { }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        // Allow Chrome extensions (chrome-extension://*) to reach localhost
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 204;
            res.Close();
            return;
        }

        try
        {
            var path = req.Url?.AbsolutePath ?? "";

            if (req.HttpMethod == "GET" && path == "/status")
            {
                await RespondJsonAsync(res, 200, new { status = "running", version = "1.0" });
                return;
            }

            if (req.HttpMethod == "POST" && path == "/exclude")
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                var body = await reader.ReadToEndAsync();

                Dictionary<string, string>? payload = null;
                try { payload = JsonSerializer.Deserialize<Dictionary<string, string>>(body); }
                catch { }

                if (payload != null
                    && payload.TryGetValue("username", out var raw)
                    && !string.IsNullOrWhiteSpace(raw))
                {
                    var username = raw.Trim().TrimStart('@');
                    await _exclusionService.AddAsync(username);
                    UserExcluded?.Invoke(username);
                    await RespondJsonAsync(res, 200, new { success = true, username });
                    return;
                }

                await RespondJsonAsync(res, 400, new { error = "username field required" });
                return;
            }

            if (req.HttpMethod == "POST" && path == "/unfollow-result")
            {
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                var body = await reader.ReadToEndAsync();

                Dictionary<string, string>? payload = null;
                try { payload = JsonSerializer.Deserialize<Dictionary<string, string>>(body); }
                catch { }

                if (payload != null
                    && payload.TryGetValue("username", out var rawUser)
                    && payload.TryGetValue("status", out var status)
                    && !string.IsNullOrWhiteSpace(rawUser))
                {
                    var username = rawUser.Trim().TrimStart('@');
                    UnfollowResultReceived?.Invoke(username, status ?? "");
                    await RespondJsonAsync(res, 200, new { success = true });
                    return;
                }

                await RespondJsonAsync(res, 400, new { error = "username and status fields required" });
                return;
            }

            res.StatusCode = 404;
            res.Close();
        }
        catch
        {
            try { res.StatusCode = 500; res.Close(); } catch { }
        }
    }

    private static async Task RespondJsonAsync(HttpListenerResponse res, int status, object data)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        res.ContentType = "application/json; charset=utf-8";
        res.ContentLength64 = bytes.Length;
        res.StatusCode = status;
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { if (_listener.IsListening) _listener.Stop(); } catch { }
        _listener.Close();
        IsRunning = false;
    }
}

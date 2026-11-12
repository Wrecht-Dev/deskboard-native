using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deskboard.Core.Models;

namespace Deskboard.Core.Api;

public sealed class TumblrException : Exception
{
    public int Status { get; }
    public TumblrException(string message, int status = 0) : base(message) => Status = status;
}

/// <summary>
/// Client for Tumblr's internal web API ("kraken").
///
/// Reads are authorised by a public bearer token that the website serves to every
/// visitor plus the signed-in user's session cookies. Writes additionally need the
/// per-session CSRF token embedded in the dashboard bootstrap.
/// </summary>
public sealed partial class TumblrClient : IDisposable
{
    public const string Origin = "https://www.tumblr.com";
    public const string ApiBase = Origin + "/api";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    private readonly string _configDir;
    private string _token = "";
    private string? _csrf;
    private readonly Dictionary<string, string> _cookies;

    public bool HasSession => _cookies.Count > 0;

    public TumblrClient(string? configDir = null, string? dataDir = null, IDictionary<string, string>? cookies = null)
    {
        _configDir = configDir ?? Paths.ConfigDir;
        _cookies = cookies is null ? CookieStore.LoadStore(dataDir) : new Dictionary<string, string>(cookies);

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = false,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36");
        // Tumblr answers snake_case unless asked for camelCase; every parser here
        // expects the camelCase dialect the website itself uses.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json;format=camelcase");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        _http.DefaultRequestHeaders.Referrer = new Uri(Origin + "/");
    }

    // ---------------------------------------------------------------- auth --
    public bool HasToken => !string.IsNullOrEmpty(_token);

    /// <summary>Loads the site's public web token from cache or the website itself.</summary>
    public async Task<bool> EnsureTokenAsync(CancellationToken ct = default)
    {
        if (HasToken) return true;

        var cacheFile = Path.Combine(_configDir, "api_token");
        try
        {
            if (File.Exists(cacheFile))
            {
                var cached = (await File.ReadAllTextAsync(cacheFile, ct)).Trim();
                if (cached.Length is > 20 and < 200) { _token = cached; return true; }
            }
        }
        catch (IOException) { /* fall through to discovery */ }

        try
        {
            using var request = NewRequest(HttpMethod.Get, Origin + "/");
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
            var html = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var text = await html.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var match = TokenRegex().Match(text);
            if (!match.Success) return false;
            _token = match.Groups[1].Value;
            Directory.CreateDirectory(_configDir);
            await File.WriteAllTextAsync(cacheFile, _token, ct).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(cacheFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>The per-session CSRF token required by write endpoints.</summary>
    public async Task<string?> CsrfTokenAsync(bool refresh = false, CancellationToken ct = default)
    {
        if (_csrf is not null && !refresh) return _csrf;

        using var request = NewRequest(HttpMethod.Get, Origin + "/dashboard");
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");
        try
        {
            var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var match = CsrfRegex().Match(html);
            if (match.Success) _csrf = match.Groups[1].Value;
            return _csrf;
        }
        catch (Exception) { return null; }
    }

    // ------------------------------------------------------------ plumbing --
    private HttpRequestMessage NewRequest(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.TryAddWithoutValidation("X-Version", "1");
        if (_cookies.Count > 0)
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}")));
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, IDictionary<string, string?>? query = null,
        IDictionary<string, string?>? form = null, CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct).ConfigureAwait(false);

        var url = path.StartsWith("http") ? path : ApiBase + (path.StartsWith('/') ? path : "/" + path);
        if (query is { Count: > 0 })
        {
            var qs = string.Join("&", query.Where(kv => kv.Value is not null)
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
            url += (url.Contains('?') ? "&" : "?") + qs;
        }

        HttpContent? content = null;
        if (form is not null)
        {
            content = new FormUrlEncodedContent(form.Where(kv => kv.Value is not null)
                .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!)));
            var csrf = await CsrfTokenAsync(ct: ct).ConfigureAwait(false);
            if (csrf is not null) content.Headers.TryAddWithoutValidation("X-CSRF", csrf);
        }

        using var request = NewRequest(method, url, content);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new TumblrException(Explain((int)response.StatusCode, body), (int)response.StatusCode);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private static string Explain(int status, string body) => status switch
    {
        401 => "Tumblr refused the request: your session expired or is missing.",
        403 => "Tumblr blocked that request.",
        404 => "Not found.",
        429 => "Rate limited by Tumblr — give it a minute.",
        >= 500 => $"Tumblr had a server error ({status}).",
        _ => $"Tumblr returned {status}."
    };

    // --------------------------------------------------------------- reading --
    private static Paged<Post> ParsePosts(JsonElement root)
    {
        var response = root.TryGetProperty("response", out var r) ? r : root;
        JsonElement list = default;
        var found = false;

        if (response.ValueKind == JsonValueKind.Array) { list = response; found = true; }
        else if (response.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "posts", "likedPosts", "elements", "objects" })
            {
                if (response.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                { list = arr; found = true; break; }
            }
            if (!found && response.TryGetProperty("timeline", out var timeline) &&
                timeline.ValueKind == JsonValueKind.Object &&
                timeline.TryGetProperty("elements", out var els) && els.ValueKind == JsonValueKind.Array)
            { list = els; found = true; }

            if (!found && response.TryGetProperty("timeline", out var t2) && t2.ValueKind == JsonValueKind.Object)
            {
                if (response.TryGetProperty("totalPosts", out var totalEl) && totalEl.TryGetInt64(out var total))
                    return new Paged<Post> { Items = new(), Total = total };
            }
        }

        var posts = new List<Post>();
        long? totalPosts = null;
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("totalPosts", out var tp) && tp.TryGetInt64(out var tpv)) totalPosts = tpv;

        if (found)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("objectType", out var ot) && ot.GetString() is { } kind && kind != "post") continue;
                var parsed = PostParser.Parse(item);
                if (parsed.BlogName.Length == 0 && parsed.Content.Count == 0 && parsed.Trail.Count == 0)
                    continue; // promo/watermark element
                posts.Add(parsed);
            }
        }

        string? next = null;
        if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("links", out var links) &&
            links.ValueKind == JsonValueKind.Object && links.TryGetProperty("next", out var n) &&
            n.ValueKind == JsonValueKind.Object && n.TryGetProperty("href", out var href))
            next = href.GetString();
        else if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("timeline", out var tl) &&
                 tl.ValueKind == JsonValueKind.Object && tl.TryGetProperty("links", out var tlinks) &&
                 tlinks.ValueKind == JsonValueKind.Object && tlinks.TryGetProperty("next", out var tn) &&
                 tn.ValueKind == JsonValueKind.Object && tn.TryGetProperty("href", out var thref))
            next = thref.GetString();

        return new Paged<Post> { Items = posts, NextUrl = next, Total = totalPosts };
    }

    public async Task<Paged<Post>> GetPageAsync(string urlOrPath, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, urlOrPath, ct: ct).ConfigureAwait(false);
        return ParsePosts(doc.RootElement);
    }

    public Task<Paged<Post>> GetHomeAsync(int limit = 14, CancellationToken ct = default)
        => GetPageAsyncParam("/v2/tabs/for_you", new Dictionary<string, string?>
        {
            ["notes_info"] = "false", ["reblog_info"] = "true", ["limit"] = limit.ToString()
        }, ct);

    public Task<Paged<Post>> GetFollowingAsync(int limit = 20, CancellationToken ct = default)
        => GetPageAsyncParam("/v2/timeline/dashboard", new Dictionary<string, string?>
        {
            ["reblog_info"] = "true", ["wrapped-timeline"] = "true", ["limit"] = limit.ToString()
        }, ct);

    public Task<Paged<Post>> GetLikesAsync(int limit = 20, CancellationToken ct = default)
        => GetPageAsyncParam("/v2/user/likes", new Dictionary<string, string?> { ["limit"] = limit.ToString() }, ct);

    public Task<Paged<Post>> GetBlogPostsAsync(string blog, int limit = 20, CancellationToken ct = default)
        => GetPageAsyncParam($"/v2/blog/{Uri.EscapeDataString(blog)}/posts",
            new Dictionary<string, string?> { ["limit"] = limit.ToString(), ["reblog_info"] = "true" }, ct);

    // The website's tag page requests the hub timeline; `sort=top` for Top.
    public Task<Paged<Post>> GetTaggedAsync(string tag, string sort = "latest", int limit = 14, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?> { ["limit"] = levelLimit(limit) };
        if (sort == "top") query["sort"] = "top";
        return GetPageAsyncParam($"/v2/hubs/{Uri.EscapeDataString(tag)}/timeline", query, ct);
        static string levelLimit(int l) => l.ToString();
    }

    public Task<Paged<Post>> GetExploreAsync(CancellationToken ct = default)
        => GetPageAsyncParam("/v2/explore/recommended",
            new Dictionary<string, string?> { ["reblog_info"] = "true", ["limit"] = "20" }, ct);

    private async Task<Paged<Post>> GetPageAsyncParam(string path, Dictionary<string, string?> query,
        CancellationToken ct)
    {
        using var doc = await SendAsync(HttpMethod.Get, path, query, ct: ct).ConfigureAwait(false);
        return ParsePosts(doc.RootElement);
    }

    public Task<Blog> GetBlogInfoAsync(string blog, CancellationToken ct = default)
        => GetJsonAsync($"/v2/blog/{Uri.EscapeDataString(blog)}/info", ct)
            .ContinueWith(t => BlogParser.Parse(t.Result), ct);

    public async Task<List<string>> GetFollowedTagsAsync(CancellationToken ct = default)
    {
        var tags = new List<string>();
        using var doc = await SendAsync(HttpMethod.Get, "/v2/user/tags", new Dictionary<string, string?> { ["limit"] = "200" }, ct: ct)
            .ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("response", out var response) &&
            response.TryGetProperty("tags", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var name = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && !tags.Contains(name)) tags.Add(name);
            }
        }
        return tags;
    }

    private async Task<JsonElement> GetJsonAsync(string path, CancellationToken ct)
    {
        using var doc = await SendAsync(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return doc.RootElement.Clone();
    }

    public void Dispose() => _http.Dispose();

    [GeneratedRegex("csrfToken\\\\{0,2}\"\\s*:\\s*\\\\{0,2}\"([^\"\\\\]{8,80})")]
    private static partial Regex CsrfRegex();

    [GeneratedRegex("API_TOKEN\\\\?\"\\s*:\\s*\\\\?\"([A-Za-z0-9]{20,80})")]
    private static partial Regex TokenRegex();
}

public static class Paths
{
    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "deskboard-native");

    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "deskboard-native");
}

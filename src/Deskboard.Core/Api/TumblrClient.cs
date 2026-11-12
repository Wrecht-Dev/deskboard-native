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
    private readonly string _dataDir;
    private bool _sessionRefreshed;
    private string _token = "";
    private string? _csrf;
    private readonly Dictionary<string, string> _cookies;

    public bool HasSession => _cookies.Count > 0;

    /// <summary>When false, a 401 never silently imports the browser session.</summary>
    public bool AutoRefreshSession { get; set; } = true;

    public TumblrClient(string? configDir = null, string? dataDir = null, IDictionary<string, string>? cookies = null)
    {
        _configDir = configDir ?? Paths.ConfigDir;
        _cookies = cookies is null ? CookieStore.LoadStore(dataDir) : new Dictionary<string, string>(cookies);
        _dataDir = dataDir ?? Paths.DataDir;

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

    /// <summary>Forget the current session (cookies + persisted store).</summary>
    public void SignOut()
    {
        _cookies.Clear();
        _sessionRefreshed = true;
        _csrf = null;
        try
        {
            var path = Path.Combine(_dataDir, "cookies.json");
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception) { }
    }

    /// <summary>Re-read the session from the user's Firefox profile (cookies rotate).</summary>
    public bool RefreshSessionFromBrowser()
    {
        if (!AutoRefreshSession || _sessionRefreshed) return false;
        _sessionRefreshed = true;
        var fresh = FirefoxCookies.ReadTumblrCookies();
        if (fresh.Count == 0) return false;
        _cookies.Clear();
        foreach (var (k, v) in fresh) _cookies[k] = v;
        CookieStore.SaveStore(_cookies, _dataDir);
        return true;
    }

    /// <summary>Notes on a post: likes, reblogs, replies, follows.</summary>
    public async Task<NotesPage> GetNotesAsync(string blog, string postId, string? url = null,
        CancellationToken ct = default)
    {
        var path = url ?? $"/v2/blog/{Uri.EscapeDataString(blog)}/notes?id={Uri.EscapeDataString(postId)}&mode=all";
        using var doc = await SendAsync(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return ParseNotes(doc.RootElement);
    }

    private static NotesPage ParseNotes(JsonElement root)
    {
        var response = root.TryGetProperty("response", out var r) ? r : root;
        var notes = new List<Note>();
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("notes", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in arr.EnumerateArray())
            {
                if (n.ValueKind != JsonValueKind.Object) continue;
                notes.Add(new Note
                {
                    Type = n.Str("type"),
                    Timestamp = n.Num("timestamp"),
                    BlogName = n.Str("blogName"),
                    BlogTitle = n.Str("blogTitle"),
                    BlogUrl = n.Str("blogUrl"),
                    AvatarUrl = n.Str("avatarUrl"),
                    Followed = n.Bool("followed"),
                    Adult = n.Bool("blogIsAdult"),
                });
            }
        }

        string? next = null;
        if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("links", out var links) &&
            links.ValueKind == JsonValueKind.Object && links.TryGetProperty("next", out var n2) &&
            n2.ValueKind == JsonValueKind.Object && n2.TryGetProperty("href", out var href))
            next = href.GetString();

        return new NotesPage
        {
            Notes = notes,
            Total = (int)(response.ValueKind == JsonValueKind.Object ? response.Num("totalNotes") : 0),
            TotalLikes = (int)(response.ValueKind == JsonValueKind.Object ? response.Num("totalLikes") : 0),
            TotalReblogs = (int)(response.ValueKind == JsonValueKind.Object ? response.Num("totalReblogs") : 0),
            TotalReplies = (int)(response.ValueKind == JsonValueKind.Object ? response.Num("totalReplies") : 0),
            NextUrl = next,
        };
    }

    /// <summary>Write request: carries the CSRF token and retries once when it is stale.</summary>
    private async Task<JsonDocument> SendWriteAsync(HttpMethod method, string path,
        IDictionary<string, string?>? query = null, IDictionary<string, string?>? form = null,
        CancellationToken ct = default)
    {
        try
        {
            return await SendAsync(method, path, query, form, ct).ConfigureAwait(false);
        }
        catch (TumblrException ex) when (ex.Status == 401 && ex.Message.Contains("1022"))
        {
            await CsrfTokenAsync(refresh: true, ct: ct).ConfigureAwait(false);
            return await SendAsync(method, path, query, form, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Low-level GET returning the decoded JSON, for schemas the typed
    /// models don't cover yet (and for diagnostics).</summary>
    public async Task<JsonElement> RawGetAsync(string path, IDictionary<string, string?>? query = null,
        CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, path, query, ct: ct).ConfigureAwait(false);
        return doc.RootElement.Clone();
    }

    // ------------------------------------------------------------------ login --
    /// <summary>
    /// Sign in with an email/username and password.
    ///
    /// The website authenticates against /v2/oauth2/token using the same public
    /// bearer token that authorises anonymous reads. On success Tumblr sets the
    /// session cookies we then use for every other call. Two-factor and captcha
    /// challenges are reported distinctly so the UI can prompt for the next step.
    /// </summary>
    public async Task<LoginResult> LoginAsync(string username, string password,
        string? tfaToken = null, string? captchaToken = null, string? blackboxSessionId = null,
        CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct).ConfigureAwait(false);
        if (!HasToken) return new LoginResult { Status = LoginStatus.Failed, Message = "Couldn't reach Tumblr." };

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username.Trim(),
            ["password"] = password,
        };
        if (!string.IsNullOrWhiteSpace(tfaToken)) form["tfa_token"] = tfaToken;
        if (!string.IsNullOrWhiteSpace(captchaToken)) form["captcha_token"] = captchaToken;
        if (!string.IsNullOrWhiteSpace(blackboxSessionId)) form["blackbox_session_id"] = blackboxSessionId;

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiBase + "/v2/oauth2/token")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.TryAddWithoutValidation("Accept", "application/json;format=camelcase");
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36");

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            // The session arrives as Set-Cookie headers; the JSON body alone is not
            // enough to use the website API.
            var fresh = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
                ? SetCookieParser.Parse(setCookies)
                : new Dictionary<string, string>();
            if (fresh.Count > 0)
            {
                foreach (var (name, value) in fresh) _cookies[name] = value;
                CookieStore.SaveStore(_cookies, _dataDir);
            }
            return new LoginResult
            {
                Status = LoginStatus.Success,
                Message = fresh.Count > 0 ? "Signed in." : "Signed in, but no session cookie was returned.",
                CookieCount = fresh.Count,
            };
        }

        var (status, message) = ClassifyLoginError((int)response.StatusCode, body);
        return new LoginResult { Status = status, Message = message };
    }

    private static (LoginStatus Status, string Message) ClassifyLoginError(int httpStatus, string body)
    {
        string error = "", description = "";
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var e)) error = e.GetString() ?? "";
            if (root.TryGetProperty("errorDescription", out var d)) description = d.GetString() ?? "";
            else if (root.TryGetProperty("error_description", out var d2)) description = d2.GetString() ?? "";
        }
        catch (JsonException) { }

        var probe = (error + " " + description).ToLowerInvariant();
        if (httpStatus == 429 || probe.Contains("rate limit"))
            return (LoginStatus.RateLimited, "Too many attempts — wait a minute and try again.");
        if (probe.Contains("captcha"))
            return (LoginStatus.NeedsCaptcha, "Tumblr wants a captcha for this attempt. Sign in through your browser once, then use “Use browser session”.");
        if (probe.Contains("2fa") || probe.Contains("two-factor") || probe.Contains("two factor") ||
            probe.Contains("tfa") || probe.Contains("verification code"))
            return (LoginStatus.NeedsTwoFactor, "Enter the two-factor code from your authenticator.");

        var friendly = string.IsNullOrWhiteSpace(description) ? "Tumblr didn't accept that login." : description;
        return (LoginStatus.BadCredentials, friendly);
    }

    // ---------------------------------------------------------------- search --
    public Task<Paged<Post>> SearchPostsAsync(string query, int limit = 20, CancellationToken ct = default)
        => GetPageAsyncParam("/v2/timeline/search",
            new Dictionary<string, string?> { ["query"] = query, ["limit"] = limit.ToString() }, ct);

    public async Task<SearchBlogs> SearchBlogsAsync(string query, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/v2/search/blogs",
            new Dictionary<string, string?> { ["query"] = query }, ct: ct).ConfigureAwait(false);
        var result = new SearchBlogs();
        var root = doc.RootElement.TryGetProperty("response", out var r) ? r : doc.RootElement;

        if (root.TryGetProperty("blogs", out var blogs) && blogs.ValueKind == JsonValueKind.Array)
            foreach (var b in blogs.EnumerateArray())
                result.Blogs.Add(BlogParser.Parse(b));

        if (root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            foreach (var t in tags.EnumerateArray())
            {
                var name = t.ValueKind == JsonValueKind.String ? t.GetString()
                    : t.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name)) result.Tags.Add(name!);
            }

        if (root.TryGetProperty("communities", out var comms) && comms.ValueKind == JsonValueKind.Array)
            foreach (var c in comms.EnumerateArray())
                result.Communities.Add(new SearchCommunity
                {
                    Name = c.Str("name"),
                    Title = c.Str("title"),
                    Description = c.Str("description"),
                    Avatar = c.Str("avatar"),
                    Members = c.Num("members"),
                });

        return result;
    }

    // --------------------------------------------------------------- composer --
    /// <summary>
    /// Builds the NPF content array the posts endpoint expects. Pure so it can be
    /// unit tested without touching the network.
    /// </summary>
    public static string BuildContentJson(ComposerDraft draft)
    {
        var blocks = new List<Dictionary<string, object>>();
        if (!string.IsNullOrWhiteSpace(draft.Text))
        {
            var block = new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = draft.Text.TrimEnd(),
            };
            if (draft.PostType == "quote") block["subtype"] = "quote";
            blocks.Add(block);
        }

        if (draft.PostType == "link" && draft.LinkUrl.Trim().Length > 0)
            blocks.Add(new Dictionary<string, object>
            {
                ["type"] = "link",
                ["url"] = draft.LinkUrl.Trim(),
                ["title"] = draft.LinkTitle.Trim().Length > 0 ? draft.LinkTitle.Trim() : draft.LinkUrl.Trim(),
            });

        foreach (var image in draft.Images.Where(i => i.Url.Length > 0))
            blocks.Add(new Dictionary<string, object>
            {
                ["type"] = "image",
                ["media"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = image.MimeType,
                        ["url"] = image.Url,
                        ["width"] = image.Width,
                        ["height"] = image.Height,
                    },
                },
            });

        return JsonSerializer.Serialize(blocks);
    }

    /// <summary>Create a post on one of the user's blogs.</summary>
    public Task CreatePostAsync(string blog, ComposerDraft draft, string state,
        string? publishOn = null, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string?>
        {
            ["content"] = BuildContentJson(draft),
            ["layout"] = JsonSerializer.Serialize(new[] { "rows" }),
            ["state"] = state,
        };
        var tags = draft.Tags.Trim();
        if (tags.Length > 0) form["tags"] = tags;
        if (publishOn is not null) form["publish_on"] = publishOn;
        return SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/posts",
            form: form, ct: ct).AsVoid();
    }

    /// <summary>Submit a post to a blog that accepts submissions.</summary>
    public Task SubmitPostAsync(string blog, ComposerDraft draft, CancellationToken ct = default)
        => CreatePostAsync(blog, draft, "submission", null, ct);

    /// <summary>Edit one of your own posts (NPF rewrite).</summary>
    public Task EditPostAsync(string blog, string postId, ComposerDraft draft, string state = "",
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string?>
        {
            ["content"] = BuildContentJson(draft),
            ["layout"] = JsonSerializer.Serialize(new[] { "rows" }),
        };
        var tags = draft.Tags.Trim();
        if (tags.Length > 0) form["tags"] = tags;
        if (state.Length > 0) form["state"] = state;
        return SendWriteAsync(HttpMethod.Put, $"/v2/blog/{Uri.EscapeDataString(blog)}/posts/{postId}",
            form: form, ct: ct).AsVoid();
    }

    /// <summary>Upload an image for the composer; returns its CDN media descriptor.</summary>
    public async Task<UploadedMedia?> UploadImageAsync(string filePath, CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var mime = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/png",
        };
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes)
        {
            Headers = { ContentType = new MediaTypeHeaderValue(mime) },
        };
        form.Add(file, "image", Path.GetFileName(filePath));

        await EnsureTokenAsync(ct).ConfigureAwait(false);
        var csrf = await CsrfTokenAsync(ct: ct).ConfigureAwait(false);
        using var request = NewRequest(HttpMethod.Post, ApiBase + "/v2/media/image", form);
        if (csrf is not null) request.Headers.TryAddWithoutValidation("X-CSRF", csrf);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new TumblrException(Explain((int)response.StatusCode, body), (int)response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement.TryGetProperty("response", out var r) ? r : doc.RootElement;
        JsonElement mediaEl = default;
        var found = false;
        if (root.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Array && media.GetArrayLength() > 0)
        { mediaEl = media[0]; found = true; }
        else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0) { mediaEl = root[0]; found = true; }
        if (!found) throw new TumblrException("Tumblr didn't return an uploaded image.");

        return new UploadedMedia
        {
            Url = mediaEl.Str("url"),
            MimeType = mediaEl.Str("type") is { Length: > 0 } t ? t : mime,
            Width = (int)mediaEl.Num("width"),
            Height = (int)mediaEl.Num("height"),
        };
    }

    /// <summary>Upload audio for the composer (field name is "audio").</summary>
    public async Task<UploadedMedia?> UploadAudioAsync(string filePath, CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes)
        {
            Headers = { ContentType = new MediaTypeHeaderValue("audio/mpeg") },
        };
        form.Add(file, "audio", Path.GetFileName(filePath));
        await EnsureTokenAsync(ct).ConfigureAwait(false);
        var csrf = await CsrfTokenAsync(ct: ct).ConfigureAwait(false);
        using var request = NewRequest(HttpMethod.Post, ApiBase + "/v2/media/audio", form);
        if (csrf is not null) request.Headers.TryAddWithoutValidation("X-CSRF", csrf);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new TumblrException(Explain((int)response.StatusCode, body), (int)response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement.TryGetProperty("response", out var r) ? r : doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("media", out var media) &&
            media.ValueKind == JsonValueKind.Object)
            root = media;
        return new UploadedMedia
        {
            Url = root.Str("url"),
            MimeType = root.Str("type") is { Length: > 0 } t ? t : "audio/mpeg",
        };
    }

    /// <summary>Read blog settings (flattened to scalar keys for display).</summary>
    public async Task<Dictionary<string, string>> GetBlogSettingsAsync(string blog, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get,
            $"/v2/blog/{Uri.EscapeDataString(blog)}/settings", ct: ct).ConfigureAwait(false);
        return FlattenSettings(doc.RootElement);
    }

    /// <summary>Write blog settings (only the supplied keys are sent).</summary>
    public Task UpdateBlogSettingsAsync(string blog, IDictionary<string, string> values,
        CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/settings",
            form: values.ToDictionary(kv => kv.Key, kv => (string?)kv.Value), ct: ct).AsVoid();

    /// <summary>Write account settings (only the supplied keys are sent).</summary>
    public Task UpdateUserSettingsAsync(IDictionary<string, string> values, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/user/settings",
            form: values.ToDictionary(kv => kv.Key, kv => (string?)kv.Value), ct: ct).AsVoid();

    /// <summary>Read account settings.</summary>
    public async Task<Dictionary<string, string>> GetUserSettingsAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/v2/user/settings", ct: ct).ConfigureAwait(false);
        return FlattenSettings(doc.RootElement);
    }

    private static Dictionary<string, string> FlattenSettings(JsonElement root)
    {
        var result = new Dictionary<string, string>();
        var response = root.TryGetProperty("response", out var r) ? r : root;
        if (response.ValueKind != JsonValueKind.Object) return result;
        foreach (var property in response.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) continue;
            result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? ""
                : property.Value.GetRawText();
        }
        return result;
    }

    /// <summary>@mention autocomplete for the composer.</summary>
    public async Task<List<string>> MentionSuggestionsAsync(string prefix, CancellationToken ct = default)
    {
        var clean = new string((prefix ?? "").Where(char.IsLetterOrDigit).ToArray());
        var path = clean.Length > 0 ? $"/v2/mention/{Uri.EscapeDataString(clean)}" : "/v2/mention";
        using var doc = await SendAsync(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        var names = new List<string>();
        var root = doc.RootElement.TryGetProperty("response", out var r) ? r : doc.RootElement;
        if (root.TryGetProperty("blogs", out var blogs) && blogs.ValueKind == JsonValueKind.Array)
            foreach (var blog in blogs.EnumerateArray())
                if (blog.Str("name") is { Length: > 0 } name && !names.Contains(name)) names.Add(name);
        return names;
    }

    /// <summary>Quick state change (publish a draft, move to draft, …).</summary>
    public Task SetPostStateAsync(string blog, string postId, string state, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/post/edit",
            form: new Dictionary<string, string?> { ["id"] = postId, ["state"] = state }, ct: ct).AsVoid();

    public Task DeleteSubmissionAsync(string blog, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Delete, $"/v2/blog/{Uri.EscapeDataString(blog)}/posts/submission",
            ct: ct).AsVoid();

    public Task DeleteInboxAsync(CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Delete, "/v2/user/inbox", ct: ct).AsVoid();

    /// <summary>Blog drafts / queue / inbox submissions.</summary>
    public Task<Paged<Post>> GetDraftsAsync(string blog, int limit = 20, CancellationToken ct = default)
        => GetPageAsyncParam($"/v2/blog/{Uri.EscapeDataString(blog)}/posts/draft",
            new Dictionary<string, string?> { ["limit"] = limit.ToString() }, ct);

    public Task<Paged<Post>> GetQueueAsync(string blog, int limit = 20, CancellationToken ct = default)
        => GetPageAsyncParam($"/v2/blog/{Uri.EscapeDataString(blog)}/posts/queue",
            new Dictionary<string, string?> { ["limit"] = limit.ToString() }, ct);

    public Task<Paged<Post>> GetInboxAsync(int limit = 20, CancellationToken ct = default)
        => GetPageAsyncParam("/v2/user/inbox",
            new Dictionary<string, string?> { ["limit"] = limit.ToString(), ["offset"] = "0" }, ct);

    // ------------------------------------------------------------ GIF library --
    public async Task<List<GifItem>> GetPopularGifsAsync(int offset = 0, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/v2/gif/popular",
            new Dictionary<string, string?> { ["offset"] = offset.ToString() }, ct: ct).ConfigureAwait(false);
        var gifs = new List<GifItem>();
        var root = doc.RootElement.TryGetProperty("response", out var r) ? r : doc.RootElement;
        if (!root.TryGetProperty("gifs", out var list) || list.ValueKind != JsonValueKind.Array) return gifs;
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("media", out var media) || media.ValueKind != JsonValueKind.Array ||
                media.GetArrayLength() == 0) continue;
            var m = media[0];
            var url = m.Str("url");
            if (url.Length == 0) continue;
            var poster = "";
            if (m.TryGetProperty("poster", out var posterEl) && posterEl.ValueKind == JsonValueKind.Object)
                poster = posterEl.Str("url");
            gifs.Add(new GifItem
            {
                Url = url,
                Poster = poster,
                Width = (int)m.Num("width"),
                Height = (int)m.Num("height"),
                MimeType = m.Str("type") is { Length: > 0 } t ? t : "image/gif",
            });
        }
        return gifs;
    }

    // ---------------------------------------------------------------- trends --
    /// <summary>Trending topics, as tag names in the order Tumblr surfaces them.</summary>
    public async Task<List<string>> GetTrendingTagsAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/v2/explore/trending/tags", ct: ct).ConfigureAwait(false);
        var tags = new List<string>();
        var root = doc.RootElement.TryGetProperty("response", out var r) ? r : doc.RootElement;
        if (root.TryGetProperty("timeline", out var timeline) &&
            timeline.TryGetProperty("elements", out var elements) &&
            elements.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in elements.EnumerateArray())
            {
                if (element.TryGetProperty("resources", out var resources) &&
                    resources.ValueKind == JsonValueKind.Array)
                {
                    foreach (var resource in resources.EnumerateArray())
                    {
                        var tag = resource.Str("tag");
                        if (tag.Length > 0 && !tags.Contains(tag)) tags.Add(tag);
                    }
                }
                if (element.Str("topicTitle") is { Length: > 0 } title && !tags.Contains(title))
                    tags.Add(title);
            }
        }
        return tags;
    }

    // ----------------------------------------------------------------- queue --
    public Task SetQueuePausedAsync(string blog, bool paused, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/posts/queue/state",
            form: new Dictionary<string, string?> { ["paused"] = paused ? "true" : "false" }, ct: ct).AsVoid();

    public Task ShuffleQueueAsync(string blog, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/posts/queue/shuffle",
            ct: ct).AsVoid();

    public Task ReorderQueueAsync(string blog, string postId, string insertAfter = "",
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string?> { ["post_id"] = postId };
        if (insertAfter.Length > 0) form["insert_after"] = insertAfter;
        return SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/posts/queue/reorder",
            form: form, ct: ct).AsVoid();
    }

    // ----------------------------------------------------------- post actions --
    public Task DeletePostAsync(Post post, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(post.BlogName)}/post/delete",
            form: PostBody(post), ct: ct).AsVoid();

    public Task MutePostAsync(Post post, bool mute, CancellationToken ct = default)
    {
        var method = mute ? HttpMethod.Post : HttpMethod.Delete;
        var form = mute
            ? new Dictionary<string, string?> { ["mute_length_seconds"] = null }
            : null;
        return SendWriteAsync(method, $"/v2/blog/{Uri.EscapeDataString(post.BlogName)}/posts/{post.Id}/mute",
            form: form, ct: ct).AsVoid();
    }

    public Task BlockBlogAsync(string blog, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/blocks",
            form: new Dictionary<string, string?>
            {
                ["blocked_tumblelog"] = blog,
                ["force"] = "true",
            }, ct: ct).AsVoid();

    public Task UnblockBlogAsync(string blog, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Delete, $"/v2/blog/{Uri.EscapeDataString(blog)}/blocks",
            form: new Dictionary<string, string?> { ["blocked_tumblelog"] = blog }, ct: ct).AsVoid();

    public Task ReportPostAsync(Post post, string reason = "other", CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/report",
            form: new Dictionary<string, string?>
            {
                ["post_id"] = post.Id,
                ["blog_name"] = post.BlogName,
                ["reason"] = reason,
            }, ct: ct).AsVoid();

    public Task PinPostAsync(Post post, bool pin, CancellationToken ct = default)
    {
        var method = pin ? HttpMethod.Post : HttpMethod.Delete;
        return SendWriteAsync(method, $"/v2/blog/{Uri.EscapeDataString(post.BlogName)}/posts/{post.Id}/pin",
            form: null, ct: ct).AsVoid();
    }

    // -------------------------------------------------- notes moderation --
    /// <summary>Remove a note (e.g. your own reply) from one of your posts.</summary>
    public Task DeleteNoteAsync(string blog, string noteBlog, long noteCreatedTime, string postId,
        CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/notes/delete",
            form: new Dictionary<string, string?>
            {
                ["note_tumblelog"] = noteBlog,
                ["note_created_time"] = noteCreatedTime.ToString(),
                ["post_id"] = postId,
            }, ct: ct).AsVoid();

    public Task FlagNoteAsync(string blog, string flaggedBlog, long noteCreatedTime = 0,
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string?>
        {
            ["flagged_note_tumblelog_name"] = flaggedBlog,
        };
        if (noteCreatedTime > 0) form["flagged_note_created_time"] = noteCreatedTime.ToString();
        return SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/notes/flag",
            form: form, ct: ct).AsVoid();
    }

    // -------------------------------------------------------------- messaging --
    /// <summary>Direct-message conversations (or message requests).</summary>
    public async Task<ConversationsPage> GetConversationsAsync(string? url = null, CancellationToken ct = default,
        bool requests = false)
    {
        var path = url ?? (requests ? "/v2/conversations/requests?limit=20" : "/v2/conversations?limit=20");
        using var doc = await SendAsync(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return ParseConversations(doc.RootElement);
    }

    private static ConversationsPage ParseConversations(JsonElement root)
    {
        var response = root.TryGetProperty("response", out var r) ? r : root;
        var conversations = new List<Conversation>();
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("conversations", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in arr.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;

                var names = new List<string>();
                var keyMap = new Dictionary<string, string>();
                if (c.TryGetProperty("participants", out var ps) && ps.ValueKind == JsonValueKind.Array)
                    foreach (var p in ps.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.Object) continue;
                        var name = p.Str("name");
                        if (name.Length == 0) continue;
                        names.Add(name);
                        var key = p.Str("key");
                        if (key.Length > 0) keyMap[key] = name;
                    }

                // The last message preview lives in `messages` (an array).
                var lastText = "";
                var updated = c.Num("lastModifiedTs");
                if (c.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array &&
                    msgs.GetArrayLength() > 0)
                {
                    var last = msgs[msgs.GetArrayLength() - 1];
                    lastText = last.Str("message");
                    var ts = last.Num("ts");
                    if (ts > updated) updated = ts;
                }

                conversations.Add(new Conversation
                {
                    Id = c.Str("id") is { Length: > 0 } id ? id : c.Str("canonicalId"),
                    Participants = names,
                    ParticipantNames = keyMap,
                    LastMessage = lastText,
                    Updated = updated,
                    Unread = (int)c.Num("unreadMessagesCount"),
                    CanSend = c.Bool("canSend", true),
                });
            }
        }

        string? next = null;
        if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("links", out var links) &&
            links.ValueKind == JsonValueKind.Object && links.TryGetProperty("next", out var n) &&
            n.ValueKind == JsonValueKind.Object && n.TryGetProperty("href", out var href))
            next = href.GetString();

        return new ConversationsPage { Conversations = conversations, NextUrl = next };
    }

    /// <summary>Messages in one conversation, oldest first.</summary>
    public async Task<List<Message>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/v2/conversations/messages",
            new Dictionary<string, string?> { ["conversation_id"] = conversationId }, ct: ct).ConfigureAwait(false);
        return ParseMessages(doc.RootElement);
    }

    private static List<Message> ParseMessages(JsonElement root)
    {
        var response = root.TryGetProperty("response", out var r) ? r : root;
        // The route nests as {messages: {data: [...]}}
        JsonElement arr = default;
        var found = false;
        if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("messages", out var messages))
        {
            if (messages.ValueKind == JsonValueKind.Array) { arr = messages; found = true; }
            else if (messages.ValueKind == JsonValueKind.Object &&
                     messages.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            { arr = data; found = true; }
        }
        if (!found) return new List<Message>();

        var list = new List<Message>();
        foreach (var m in arr.EnumerateArray())
        {
            if (m.ValueKind != JsonValueKind.Object) continue;
            list.Add(new Message
            {
                Id = m.Str("id"),
                Participant = m.Str("participant"),
                Text = m.Str("message") is { Length: > 0 } text ? text : m.Str("text"),
                Timestamp = m.Num("ts") is var ts && ts > 0 ? ts : m.Num("timestamp") * 1000,
                ParticipantKey = m.Str("participant"),
                Type = m.Str("type") is { Length: > 0 } t ? t : "text",
                IsMine = m.Bool("isMine") || m.Bool("mine"),
            });
        }
        list.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return list;
    }

    /// <summary>Accept a message request so it becomes a normal conversation.</summary>
    public Task AcceptConversationAsync(string conversationId, string participant,
        CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/conversations/accept",
            form: new Dictionary<string, string?>
            {
                ["conversation_id"] = conversationId,
                ["participant"] = participant,
            }, ct: ct).AsVoid();

    /// <summary>Send a direct message to a participant (new or existing conversation).</summary>
    public Task SendMessageAsync(string participant, string text, string? conversationId = null,
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string?>
        {
            ["message"] = text,
            ["participants"] = participant,
            ["type"] = "TEXT",
        };
        if (!string.IsNullOrEmpty(conversationId)) form["conversation_id"] = conversationId;
        return SendWriteAsync(HttpMethod.Post, "/v2/conversations/messages", form: form, ct: ct).AsVoid();
    }

    // ----------------------------------------------------------- notifications --
    public async Task<NotificationsPage> GetNotificationsAsync(string blog, string? url = null,
        CancellationToken ct = default)
    {
        var path = url ?? $"/v2/blog/{Uri.EscapeDataString(blog)}/notifications?limit=20";
        using var doc = await SendAsync(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return ParseNotifications(doc.RootElement);
    }

    private static NotificationsPage ParseNotifications(JsonElement root)
    {
        var response = root.TryGetProperty("response", out var r) ? r : root;
        var notifications = new List<Notification>();
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("notifications", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in arr.EnumerateArray())
            {
                if (n.ValueKind != JsonValueKind.Object) continue;
                // Activity uses fromTumblelog* rather than the note/blog field names.
                var blogName = n.Str("fromTumblelogName");
                if (blogName.Length == 0) blogName = n.Str("blogName");

                var avatar = n.Str("avatarUrl");
                if (avatar.Length == 0 && n.TryGetProperty("fromTumblelogAvatars", out var avatars) &&
                    avatars.ValueKind == JsonValueKind.Array)
                {
                    var best = avatars.EnumerateArray()
                        .Where(a => a.Str("url").Length > 0)
                        .OrderByDescending(a => a.Num("width"))
                        .FirstOrDefault();
                    if (best.ValueKind == JsonValueKind.Object) avatar = best.Str("url");
                }

                notifications.Add(new Notification
                {
                    Type = n.Str("type"),
                    Timestamp = n.Num("timestamp"),
                    BlogName = blogName,
                    BlogTitle = n.Str("blogTitle"),
                    BlogUrl = n.Str("blogUrl"),
                    AvatarUrl = avatar,
                    PostId = n.Str("postId") is { Length: > 0 } pid ? pid : n.Str("targetPostId"),
                    ReplyText = n.Str("replyText"),
                    AskQuestion = n.Str("askQuestion"),
                    Followed = n.Bool("followed"),
                    FollowingYou = n.Bool("followingYou") || n.Bool("isFollowingYou"),
                });
            }
        }

        string? next = null;
        if (response.ValueKind == JsonValueKind.Object && response.TryGetProperty("links", out var links) &&
            links.ValueKind == JsonValueKind.Object && links.TryGetProperty("next", out var n2) &&
            n2.ValueKind == JsonValueKind.Object && n2.TryGetProperty("href", out var href))
            next = href.GetString();

        return new NotificationsPage
        {
            Notifications = notifications,
            NextUrl = next,
            Total = response.ValueKind == JsonValueKind.Object ? (int)response.Num("total") : 0,
        };
    }

    // --------------------------------------------------------------- identity --
    public async Task<(string Name, List<string> Blogs)> GetIdentityAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/v2/user/info", ct: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        var user = root.TryGetProperty("response", out var r) && r.TryGetProperty("user", out var u) ? u : root;
        var name = user.Str("name");
        var blogs = new List<string>();
        if (user.TryGetProperty("blogs", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var b in arr.EnumerateArray())
                if (b.Str("name") is { Length: > 0 } n && !blogs.Contains(n)) blogs.Add(n);
        return (name, blogs);
    }

    // ------------------------------------------------------------ write actions --
    private static Dictionary<string, string?> PostBody(Post post) => new()
    {
        ["id"] = post.Id,
        ["reblog_key"] = post.ReblogKey,
    };

    public Task LikeAsync(Post post, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/user/like", form: PostBody(post), ct: ct).AsVoid();

    public Task UnlikeAsync(Post post, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/user/unlike", form: PostBody(post), ct: ct).AsVoid();

    public Task ReblogAsync(Post post, string blog, string comment = "", string tags = "",
        string? publishOn = null, CancellationToken ct = default)
    {
        var form = PostBody(post);
        form["comment"] = comment;
        if (tags.Length > 0) form["tags"] = tags;
        if (publishOn is not null) form["publish_on"] = publishOn;
        return SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/post/reblog", form: form, ct: ct).AsVoid();
    }

    public Task FollowAsync(string blog, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/user/follow",
            form: new Dictionary<string, string?> { ["url"] = blog }, ct: ct).AsVoid();

    public Task UnfollowAsync(string blog, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/user/unfollow",
            form: new Dictionary<string, string?> { ["url"] = blog }, ct: ct).AsVoid();

    public Task ReplyAsync(Post post, string blog, string text, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, "/v2/user/post/reply", form: new Dictionary<string, string?>
        {
            ["post_id"] = post.Id,
            ["reply_text"] = text,
            ["reblog_key"] = post.ReblogKey,
            ["tumblelog"] = blog,
            ["reply_as"] = blog,
        }, ct: ct).AsVoid();

    public Task SendAskAsync(string blog, string question, bool anonymous = false, CancellationToken ct = default)
        => SendWriteAsync(HttpMethod.Post, $"/v2/blog/{Uri.EscapeDataString(blog)}/ask",
            form: new Dictionary<string, string?>
            {
                ["question"] = question,
                ["anonymous"] = anonymous ? "true" : "false",
            }, ct: ct).AsVoid();

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path,
        IDictionary<string, string?>? query = null, IDictionary<string, string?>? form = null,
        CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct).ConfigureAwait(false);

        var url = path.StartsWith("http") ? path : ApiBase + (path.StartsWith('/') ? path : "/" + path);
        if (query is { Count: > 0 })
        {
            var qs = string.Join("&", query.Where(kv => kv.Value is not null)
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
            url += (url.Contains('?') ? "&" : "?") + qs;
        }

        for (var attempt = 0; ; attempt++)
        {
            HttpContent? content = null;
            if (form is not null)
            {
                content = new FormUrlEncodedContent(form.Where(kv => kv.Value is not null)
                    .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!)));
                var csrf = await CsrfTokenAsync(ct: ct).ConfigureAwait(false);
                if (csrf is not null) content.Headers.TryAddWithoutValidation("X-CSRF", csrf);
            }

            using var request = NewRequest(method, url, content);
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                throw new TumblrException("You appear to be offline — check your connection and retry.", 0);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TumblrException("Tumblr timed out — retry in a moment.", 0);
            }
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);

            var status = (int)response.StatusCode;
            if (status == 401 && attempt == 0 && RefreshSessionFromBrowser())
                continue; // cookies refreshed from the browser; retry once

            throw new TumblrException(Explain(status, body), status);
        }
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

namespace Deskboard.Core.Api;

public enum LoginStatus { Success, NeedsTwoFactor, NeedsCaptcha, BadCredentials, RateLimited, Failed }

public sealed class LoginResult
{
    public LoginStatus Status { get; init; }
    public string Message { get; init; } = "";
    public int CookieCount { get; init; }

    public bool Succeeded => Status == LoginStatus.Success;
}

/// <summary>Helpers for parsing Set-Cookie headers from the login response.</summary>
public static class SetCookieParser
{
    /// <summary>Collects name=value pairs from raw Set-Cookie header values.</summary>
    public static Dictionary<string, string> Parse(IEnumerable<string> headers)
    {
        var jar = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header)) continue;
            var first = header.Split(';', 2)[0];
            var eq = first.IndexOf('=');
            if (eq <= 0) continue;
            var name = first[..eq].Trim();
            var value = first[(eq + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0) continue;
            // ignore deletion cookies
            if (value is "deleted" or "undefined") continue;
            jar[name] = value;
        }
        return jar;
    }
}

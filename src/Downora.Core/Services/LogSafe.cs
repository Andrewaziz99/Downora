using System.Text;

namespace Downora.Core.Services;

/// <summary>Makes values safe to write to the log file: links can carry sign-in tokens and must not be stored in full.</summary>
public static class LogSafe
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "token", "access_token", "refresh_token", "id_token", "auth", "authorization", "key", "api_key", "apikey",
        "sig", "signature", "secret", "password", "passwd", "pwd", "session", "sessionid", "sid", "code", "ticket",
        "oauth_token", "x-amz-signature", "x-amz-credential", "x-amz-security-token", "policy", "expires",
    };

    /// <summary>
    /// The link without user name and password, and with the values of token-like query parameters replaced by "…".
    /// Anything that isn't an absolute web link is reduced to a placeholder.
    /// </summary>
    public static string Url(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return "[not a web link]";
        }

        var builder = new StringBuilder();
        builder.Append(uri.Scheme).Append("://").Append(uri.Host);
        if (!uri.IsDefaultPort)
        {
            builder.Append(':').Append(uri.Port);
        }

        builder.Append(uri.AbsolutePath);
        var query = uri.Query.TrimStart('?');
        if (query.Length > 0)
        {
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(part =>
            {
                var eq = part.IndexOf('=', StringComparison.Ordinal);
                var name = eq < 0 ? part : part[..eq];
                return eq >= 0 && SensitiveNames.Contains(Uri.UnescapeDataString(name)) ? name + "=…" : part;
            });
            builder.Append('?').AppendJoin('&', parts);
        }

        // The fragment is never sent to a server and can hold anything (including tokens), so it is dropped.
        return builder.ToString();
    }
}

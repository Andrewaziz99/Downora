using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;

namespace Downora.Core.Services;

/// <summary>
/// Accepts only absolute http(s) links. A bare "youtube.com/watch?v=…" gets https:// added.
/// Validation happens before any process runs; the URL is then passed after "--" so it can never be read as an option.
/// </summary>
public sealed class UrlValidator(ISourceDetector sources) : IUrlValidator
{
    public const int MaxLength = 2048;

    public UrlValidationResult Validate(string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return new(UrlProblem.Empty);
        }

        if (text.Length > MaxLength)
        {
            return new(UrlProblem.TooLong);
        }

        if (text.Any(char.IsWhiteSpace) || text.Any(char.IsControl) || text.StartsWith('-'))
        {
            return new(UrlProblem.NotAUrl);
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            // "youtube.com/watch?v=…" or "www.example.com/a.mp4": assume https.
            if (!LooksLikeHost(text))
            {
                return new(UrlProblem.NotAUrl);
            }

            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var url))
        {
            return new(UrlProblem.NotAUrl);
        }

        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return new(UrlProblem.UnsupportedScheme);
        }

        if (string.IsNullOrEmpty(url.Host) || (!url.Host.Contains('.', StringComparison.Ordinal) && url.HostNameType == UriHostNameType.Dns))
        {
            return new(UrlProblem.NotAUrl);
        }

        return new(
            UrlProblem.None,
            url,
            sources.Normalize(url),
            sources.Detect(url),
            PreferSingleItem: sources.IsSingleItemInPlaylist(url));
    }

    private static bool LooksLikeHost(string text)
    {
        var host = text.Split('/', 2)[0];
        var dot = host.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 && dot < host.Length - 1 && !host.Contains(':', StringComparison.Ordinal);
    }
}

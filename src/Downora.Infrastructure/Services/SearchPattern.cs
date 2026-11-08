namespace Downora.Infrastructure.Services;

/// <summary>Builds a SQL LIKE "contains" pattern from user text, escaping the LIKE wildcards.</summary>
internal static class SearchPattern
{
    public const string Escape = "\\";

    public static string? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}

using System.Text;

namespace Downora.Core.Services;

/// <summary>Builds file names that Windows accepts from titles that came from the internet.</summary>
public static class FileNames
{
    public const string DefaultTemplate = "{title}";

    /// <summary>Long enough for any real title, short enough that a full path stays under Windows' 260-character limit.</summary>
    public const int MaxBaseNameLength = 120;

    private const string Fallback = "download";

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// A name safe to use on Windows: invalid characters, control characters and bidirectional marks are replaced,
    /// trailing dots and spaces dropped, reserved device names (CON, NUL, COM1…) renamed and the length capped.
    /// Never returns an empty string.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Fallback;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in ReplaceBrokenSurrogates(name).Normalize(NormalizationForm.FormC))
        {
            builder.Append(IsUnsafe(c) ? ' ' : c);
        }

        var text = CollapseSpaces(builder.ToString()).Trim().TrimEnd('.', ' ');
        if (text.Length > MaxBaseNameLength)
        {
            text = TrimToLength(text, MaxBaseNameLength).TrimEnd('.', ' ');
        }

        if (text.Length == 0)
        {
            return Fallback;
        }

        // "CON" and "CON.txt" are both the console device on Windows: look at the part before the first dot.
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var stem = dot < 0 ? text : text[..dot];
        return ReservedNames.Contains(stem.TrimEnd()) ? "_" + text : text;
    }

    /// <summary>
    /// Fills a template such as <c>{uploader} - {title}</c>. Known tokens: <c>{title}</c>, <c>{uploader}</c>, <c>{quality}</c>.
    /// Unknown tokens are removed. The result is sanitized.
    /// </summary>
    public static string Expand(string? template, string? title, string? uploader, string? quality)
    {
        var text = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        text = text
            .Replace("{title}", title ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{uploader}", uploader ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{quality}", quality ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Anything still in braces is an unknown token, which would otherwise end up in the file name.
        var start = text.IndexOf('{', StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = text.IndexOf('}', start + 1);
            if (end < 0)
            {
                break;
            }

            text = text.Remove(start, end - start + 1);
            start = text.IndexOf('{', StringComparison.Ordinal);
        }

        return Sanitize(text);
    }

    /// <summary>
    /// Returns <paramref name="baseName"/>, or "name (2)", "name (3)"… when <paramref name="isTaken"/> says the name is in use,
    /// so a new download never overwrites an older file.
    /// </summary>
    public static string MakeUnique(string baseName, Func<string, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(isTaken);
        if (!isTaken(baseName))
        {
            return baseName;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseName} ({n})";
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool IsUnsafe(char c) =>
        c < ' ' ||
        c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' ||
        // Direction marks and other invisible formatting could make a name display misleadingly.
        c is '‎' or '‏' or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩') or '​' or '﻿';

    private static string CollapseSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var c in text)
        {
            var space = char.IsWhiteSpace(c);
            if (space && lastWasSpace)
            {
                continue;
            }

            builder.Append(space ? ' ' : c);
            lastWasSpace = space;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Titles come from JSON, which may hold half an emoji (an unpaired surrogate). Such text is not valid Unicode and
    /// makes normalization throw, so each broken half becomes a space.
    /// </summary>
    private static string ReplaceBrokenSurrogates(string text)
    {
        StringBuilder? fixedText = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var broken = char.IsHighSurrogate(c)
                ? i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1])
                : char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(text[i - 1]));
            if (broken)
            {
                fixedText ??= new StringBuilder(text);
                fixedText[i] = ' ';
            }
        }

        return fixedText?.ToString() ?? text;
    }

    /// <summary>Cuts to a length without splitting a surrogate pair (emoji).</summary>
    private static string TrimToLength(string text, int length) =>
        char.IsHighSurrogate(text[length - 1]) ? text[..(length - 1)] : text[..length];
}

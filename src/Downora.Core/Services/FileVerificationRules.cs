using Downora.Core.Enums;
using Downora.Core.Interfaces;

namespace Downora.Core.Services;

/// <summary>What makes a downloaded file acceptable. Pure, so the rules are tested without any tool.</summary>
public static class FileVerificationRules
{
    /// <summary>A file this much shorter than the analysis said (and more than <see cref="MinimumShortfall"/>) was cut off.</summary>
    public const double AllowedShortfallFraction = 0.10;

    public static readonly TimeSpan MinimumShortfall = TimeSpan.FromSeconds(5);

    public static FileVerification Evaluate(MediaProbe probe, MediaKind kind, TimeSpan? expectedDuration)
    {
        ArgumentNullException.ThrowIfNull(probe);

        if (kind == MediaKind.Audio && !probe.HasAudio)
        {
            return FileVerification.Bad("The file has no audio.");
        }

        if (kind == MediaKind.Video && !probe.HasVideo)
        {
            return FileVerification.Bad("The file has no video.");
        }

        if (probe.Duration is not { } actual || actual <= TimeSpan.Zero)
        {
            return FileVerification.Bad("The file has no playable length.");
        }

        if (expectedDuration is { } expected && expected > TimeSpan.Zero)
        {
            var allowed = TimeSpan.FromTicks(Math.Max(MinimumShortfall.Ticks, (long)(expected.Ticks * AllowedShortfallFraction)));
            if (expected - actual > allowed)
            {
                return FileVerification.Bad("The file is shorter than the video, so the download was probably cut off.");
            }
        }

        return FileVerification.Ok;
    }
}

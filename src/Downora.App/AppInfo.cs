using System.Reflection;

namespace Downora.App;

internal static class AppInfo
{
    /// <summary>The product version, without the source-revision suffix the SDK appends.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? informational[..plus] : informational;
    }
}

namespace Downora.Core.Services;

/// <summary>The command Windows runs at sign-in. The argument tells Downora it started by itself, not because the user opened it.</summary>
public static class StartupCommand
{
    public const string Argument = "--startup";

    /// <summary>The path is quoted, so folders with spaces work and nothing after it can be read as another program.</summary>
    public static string Build(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\" {Argument}";
    }

    public static bool IsStartupLaunch(IEnumerable<string> args) =>
        args.Any(a => string.Equals(a, Argument, StringComparison.OrdinalIgnoreCase));
}

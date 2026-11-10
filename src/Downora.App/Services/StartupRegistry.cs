using Downora.Core.Interfaces;
using Microsoft.Win32;

namespace Downora.App.Services;

/// <summary>Windows' per-user "run at sign-in" list (HKCU, so no administrator rights are needed).</summary>
internal sealed class StartupRegistry : IStartupRegistry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Downora";

    public string? GetCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    public void SetCommand(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (command is null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
    }
}

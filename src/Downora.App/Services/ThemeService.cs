using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Downora.Core.Enums;
using Downora.Presentation.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Downora.App.Services;

/// <summary>
/// Applies Dark / Light / System to WPF-UI's controls and to Downora's own palette (Palette.Dark/Light.xaml),
/// keeps the teal brand accent, and follows Windows live while the System option is selected.
/// </summary>
internal sealed class ThemeService : IThemeService
{
    private const string PaletteFolder = "pack://application:,,,/Downora;component/Resources/Theme/";
    private static readonly Color BrandAccent = Color.FromRgb(0x00, 0xC2, 0xB8);

    private Window? _window;
    private bool _watching;

    public AppTheme Theme { get; private set; } = AppTheme.System;

    public bool IsDark { get; private set; } = true;

    public event EventHandler? EffectiveThemeChanged;

    /// <summary>Call once with the main window before <see cref="Apply"/> so System mode can follow Windows.</summary>
    public void Attach(Window window)
    {
        _window = window;
        ApplicationThemeManager.Changed += OnWpfUiThemeChanged;
    }

    public void Apply(AppTheme theme)
    {
        Theme = theme;
        var effective = theme switch
        {
            AppTheme.Dark => ApplicationTheme.Dark,
            AppTheme.Light => ApplicationTheme.Light,
            _ => WindowsUsesLightTheme() ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };

        ApplicationThemeManager.Apply(effective, WindowBackdropType.None, updateAccent: false);
        SetEffective(effective);
        UpdateSystemWatch();
    }

    private void OnWpfUiThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        // Fires when Windows switches while we follow the system theme.
        if (Theme == AppTheme.System && currentApplicationTheme is ApplicationTheme.Dark or ApplicationTheme.Light)
        {
            SetEffective(currentApplicationTheme);
        }
    }

    private void SetEffective(ApplicationTheme effective)
    {
        var dark = effective != ApplicationTheme.Light;
        ApplicationAccentColorManager.Apply(BrandAccent, effective, false, false);
        SwapPalette(dark);

        var changed = dark != IsDark;
        IsDark = dark;
        if (changed)
        {
            EffectiveThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void SwapPalette(bool dark)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Palette.", StringComparison.Ordinal) == true);
        var next = new ResourceDictionary { Source = new Uri(PaletteFolder + (dark ? "Palette.Dark.xaml" : "Palette.Light.xaml"), UriKind.Absolute) };

        if (current is null)
        {
            dictionaries.Add(next);
        }
        else if (current.Source!.OriginalString != next.Source.OriginalString)
        {
            dictionaries[dictionaries.IndexOf(current)] = next;
        }
    }

    private void UpdateSystemWatch()
    {
        if (_window is null)
        {
            return;
        }

        // The watcher hooks the native window, which only exists once the window has been shown.
        if (new WindowInteropHelper(_window).Handle == IntPtr.Zero)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window.SourceInitialized += OnSourceInitialized;
            return;
        }

        if (Theme == AppTheme.System && !_watching)
        {
            SystemThemeWatcher.Watch(_window, WindowBackdropType.None, false);
            _watching = true;
        }
        else if (Theme != AppTheme.System && _watching)
        {
            SystemThemeWatcher.UnWatch(_window);
            _watching = false;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _window!.SourceInitialized -= OnSourceInitialized;
        UpdateSystemWatch();
    }

    /// <summary>Windows' "Choose your app mode" setting (Settings › Personalization › Colors).</summary>
    private static bool WindowsUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
    }
}

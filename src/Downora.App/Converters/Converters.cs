using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Downora.App.Converters;

/// <summary>
/// Turns a URL or file path into a decoded thumbnail, or null when there is none.
/// Never throws: a broken thumbnail shows the placeholder instead of a binding error.
/// The converter parameter is the decode width in pixels (keeps memory low in long lists).
/// </summary>
public sealed class ThumbnailConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string { Length: > 0 } source)
        {
            return null;
        }

        Uri uri;
        if (Uri.TryCreate(source, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp))
        {
            uri = parsed;
        }
        else if (Path.IsPathFullyQualified(source) && File.Exists(source))
        {
            uri = new Uri(source, UriKind.Absolute);
        }
        else
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (int.TryParse(parameter as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) && width > 0)
            {
                image.DecodePixelWidth = width;
            }

            image.EndInit();
            if (image.CanFreeze)
            {
                image.Freeze();
            }

            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the value is true (or, with parameter "Invert", when false).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when a count is above zero; with parameter "Invert", visible only when it is zero (empty-state messages).</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var any = value is int count && count > 0;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            any = !any;
        }

        return any ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Collapsed for null, empty strings and zero counts.</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        null => Visibility.Collapsed,
        string s when string.IsNullOrWhiteSpace(s) => Visibility.Collapsed,
        int i when i <= 0 => Visibility.Collapsed,
        _ => Visibility.Visible,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts a Fluent System Icons name (string) to the icon enum, falling back to an empty glyph.</summary>
public sealed class SymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string name && Enum.TryParse<Wpf.Ui.Controls.SymbolRegular>(name, out var symbol)
            ? symbol
            : Wpf.Ui.Controls.SymbolRegular.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Picks the media-kind glyph for a thumbnail placeholder.</summary>
public sealed class MediaGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Wpf.Ui.Controls.SymbolRegular.MusicNote224 : Wpf.Ui.Controls.SymbolRegular.Video24;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

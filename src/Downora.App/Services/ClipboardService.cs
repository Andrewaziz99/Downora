using System.Runtime.InteropServices;
using System.Windows;
using Downora.Presentation.Services;

namespace Downora.App.Services;

internal sealed class ClipboardService : IClipboardService
{
    public string? GetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (COMException)
        {
            // Another app is holding the clipboard open; the user can simply press Paste again.
            return null;
        }
    }
}

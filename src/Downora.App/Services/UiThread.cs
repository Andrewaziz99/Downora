using System.Windows;
using Downora.Presentation.Services;

namespace Downora.App.Services;

/// <summary>Moves work to the WPF dispatcher thread. Downloads report from background threads; the window changes only here.</summary>
internal sealed class UiThread : IUiThread
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}

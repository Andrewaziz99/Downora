using Downora.Core.Models;

namespace Downora.Core.Interfaces;

/// <summary>Loads, validates and persists <see cref="AppSettings"/>.</summary>
public interface ISettingsService
{
    /// <summary>A copy of the current settings. Edit the copy and pass it to <see cref="SaveAsync"/>.</summary>
    AppSettings Current { get; }

    /// <summary>Reads settings from the database, creating defaults on first run.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and saves. Throws <see cref="Services.SettingsValidationException"/> when invalid.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    event EventHandler<AppSettings>? Changed;
}

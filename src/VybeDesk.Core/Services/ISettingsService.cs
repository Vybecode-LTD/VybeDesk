using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>Loads and persists non-secret <see cref="AppSettings"/>.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }
    void Save(AppSettings settings);
}

namespace Odin.Api.Services.AppSettings;

/// <summary>
/// Runtime-tunable feature flags and app-level toggles. Reads are memory-cached so hot paths
/// can call <see cref="GetBoolAsync"/> on every request without hitting the DB.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>Returns the boolean value of <paramref name="key"/>, or <paramref name="defaultValue"/> when unset.</summary>
    Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts the named setting. Invalidates the cached value.</summary>
    Task SetBoolAsync(string key, bool value, string? updatedBy, CancellationToken cancellationToken = default);
}

/// <summary>Well-known setting keys. Add new entries here so the contract is discoverable.</summary>
public static class AppSettingKeys
{
}

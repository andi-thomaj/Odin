namespace Odin.Api.Endpoints.AppSettingsManagement.Models;

public sealed record AppSettingsResponse(IReadOnlyDictionary<string, string> Settings);

public sealed record UpdateBoolSettingRequest(bool Enabled);

public sealed record AppSettingResponse(string Key, string Value);

using System.Security.Claims;
using Odin.Api.Endpoints.AppSettingsManagement.Models;
using Odin.Api.Services.AppSettings;

namespace Odin.Api.Endpoints.AppSettingsManagement;

/// <summary>
/// Admin-only key/value settings store for runtime-tunable feature flags
/// (e.g. <c>AdminCanSkipPayment</c>).
/// </summary>
public static class AppSettingsEndpoints
{
    public static void MapAppSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/admin/settings")
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict")
            .WithTags("App Settings");

        group.MapGet("/", GetAll).WithSummary("Returns all key/value app settings.");
        group.MapGet("/{key}/bool", GetBool).WithSummary("Returns the boolean value of a setting.");
        group.MapPut("/{key}/bool", PutBool).WithSummary("Upserts the boolean value of a setting.");
    }

    private static async Task<IResult> GetAll(IAppSettingsService settings, CancellationToken ct)
    {
        var all = await settings.GetAllAsync(ct);
        return Results.Ok(new AppSettingsResponse(all));
    }

    private static async Task<IResult> GetBool(string key, IAppSettingsService settings, CancellationToken ct)
    {
        var defaultValue = ResolveDefault(key);
        var value = await settings.GetBoolAsync(key, defaultValue, ct);
        return Results.Ok(new AppSettingResponse(key, value ? "true" : "false"));
    }

    private static async Task<IResult> PutBool(
        string key,
        UpdateBoolSettingRequest request,
        HttpContext httpContext,
        IAppSettingsService settings,
        CancellationToken ct)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");
        await settings.SetBoolAsync(key, request.Enabled, identityId, ct);
        return Results.Ok(new AppSettingResponse(key, request.Enabled ? "true" : "false"));
    }

    /// <summary>Defaults for known keys so reads stay deterministic before any admin has flipped the switch.</summary>
    internal static bool ResolveDefault(string key) => key switch
    {
        AppSettingKeys.AdminCanSkipPayment => true,
        _ => false,
    };
}

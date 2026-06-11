using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.Admin;

public static class CacheAdminEndpoints
{
    public static void MapCacheAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/cache")
            .RequireAuthorization("AdminOnly");

        // Evicts the entire in-process backend cache. Admin-only maintenance action surfaced as a
        // button on the admin jobs page — e.g. to force-refresh stale reference data or cached
        // qpAdm/G25 result payloads after a manual data fix.
        endpoints.MapPost("/clear", ClearCache)
            .RequireRateLimiting("strict")
            .Produces<ClearBackendCacheContract.Response>(StatusCodes.Status200OK);
    }

    private static IResult ClearCache(IBackendCacheMaintenanceService cacheMaintenance)
    {
        var entriesCleared = cacheMaintenance.ClearAll();
        return Results.Ok(new ClearBackendCacheContract.Response { EntriesCleared = entriesCleared });
    }
}

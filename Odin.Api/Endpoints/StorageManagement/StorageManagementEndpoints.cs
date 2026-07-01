namespace Odin.Api.Endpoints.StorageManagement;

/// <summary>Admin storage maintenance. The orphan-cleanup sweep also runs daily as a Hangfire recurring job; this
/// endpoint lets an admin trigger it (and dry-run it first) on demand.</summary>
public static class StorageManagementEndpoints
{
    public static void MapStorageManagementEndpoints(this IEndpointRouteBuilder app)
    {
        // POST api/admin/storage/cleanup-orphans?dryRun=true|false  — delete R2 data for users no longer in the DB.
        // Defaults to a DRY RUN (reports what would be deleted) so an accidental call can't wipe data; pass
        // dryRun=false to actually delete.
        app.MapPost("api/admin/storage/cleanup-orphans", CleanupOrphans)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict")
            .Produces<R2CleanupReport>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CleanupOrphans(
        IR2OrphanCleanupService service, bool? dryRun, CancellationToken cancellationToken)
        => Results.Ok(await service.RunAsync(dryRun ?? true, cancellationToken));
}

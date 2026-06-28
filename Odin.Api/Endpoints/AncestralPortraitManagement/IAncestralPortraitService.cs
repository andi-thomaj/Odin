using Odin.Api.Endpoints.AncestralPortraitManagement.Models;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <summary>
/// The paid "Through the Ages" ancestral-portrait add-on: unlock (validate the StoreKit add-on purchase), generate
/// (per era, reimagine the user's face as the era's top ancestral population via <c>gpt-image-2</c> edits), list every
/// iteration, toggle which variations the user keeps, and stream a portrait's private bytes. An order can hold MANY
/// sets (each re-purchase = a new iteration). Generation runs on a Hangfire worker; the iOS client polls.
/// </summary>
public interface IAncestralPortraitService
{
    /// <summary>Validate the add-on purchase + record a NEW set (each Apple transaction = a new iteration; a replayed
    /// transaction is idempotent) and kick off generation. StatusCode 201 (new) / 200 (transaction replay) / 403 (not
    /// the caller's order) / 404 (order missing). Throws <see cref="Payments.Models.AppStorePurchaseException"/> on an
    /// invalid purchase (mapped to 400).</summary>
    Task<(AncestralPortraitSetContract.Response Response, int StatusCode, string? Error)> PurchaseAsync(
        int orderId, string identityId, string transactionJws, CancellationToken cancellationToken = default);

    /// <summary>All iterations for an order, newest first (empty list when none bought). 403/404 mapped via StatusCode.</summary>
    Task<(List<AncestralPortraitSetContract.Response>? Response, int StatusCode)> ListSetsAsync(
        int orderId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>A single set by id (for polling one iteration). StatusCode 200 / 403 / 404.</summary>
    Task<(AncestralPortraitSetContract.Response? Response, int StatusCode)> GetSetByIdAsync(
        Guid setId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Re-enqueue generation for a specific set (e.g. after the user captures their face, or to retry a
    /// failed iteration). StatusCode 202 / 403 / 404.</summary>
    Task<int> RequestGenerateAsync(Guid setId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Delete one iteration: removes its PRIVATE portrait objects from R2 AND the rows. 200 / 409 (still
    /// generating — can't delete mid-run) / 403 / 404.</summary>
    Task<int> DeleteSetAsync(Guid setId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Toggle this variation's selection (multi-select — any subset per era, even all/none). 200 / 403 / 404.</summary>
    Task<int> SelectAsync(int portraitId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>One portrait's bytes if it belongs to the caller. StatusCode 200 / 403 / 404.</summary>
    Task<(byte[]? Bytes, string? ContentType, int StatusCode)> GetPortraitBytesAsync(
        int portraitId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>The actual generation loop (called by the Hangfire worker). Bounded, never rethrows — records Status.</summary>
    Task RunGenerationAsync(Guid setId, CancellationToken cancellationToken = default);

    /// <summary>Admin aggregate: total runs / images / tokens / estimated USD spend across ALL portrait runs.</summary>
    Task<AncestralPortraitUsageContract.Response> GetUsageSummaryAsync(CancellationToken cancellationToken = default);
}

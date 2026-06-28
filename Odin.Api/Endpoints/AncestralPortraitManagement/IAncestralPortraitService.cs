using Odin.Api.Endpoints.AncestralPortraitManagement.Models;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <summary>
/// The paid "Through the Ages" ancestral-portrait add-on: unlock (validate the StoreKit add-on purchase), generate
/// (per era, reimagine the user's face as the era's top ancestral population via <c>gpt-image-2</c> edits), list, pick
/// a variation per era, and stream a portrait's private bytes. Generation runs on a Hangfire worker; the iOS client polls.
/// </summary>
public interface IAncestralPortraitService
{
    /// <summary>Validate the add-on purchase + record the set (idempotent on the Apple transaction id) and kick off
    /// generation. StatusCode 201 (new) / 200 (already unlocked) / 403 (not the caller's order) / 404 (order missing).
    /// Throws <see cref="Payments.Models.AppStorePurchaseException"/> on an invalid purchase (mapped to 400).</summary>
    Task<(AncestralPortraitSetContract.Response Response, int StatusCode, string? Error)> PurchaseAsync(
        int orderId, string identityId, string transactionJws, CancellationToken cancellationToken = default);

    /// <summary>The set for an order (Status "NotPurchased" when unbought). 403/404 mapped via StatusCode.</summary>
    Task<(AncestralPortraitSetContract.Response? Response, int StatusCode)> GetSetAsync(
        int orderId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Re-enqueue generation for an already-purchased set (e.g. after the user captures their face, or to
    /// retry a failed run). StatusCode 202 / 403 / 404.</summary>
    Task<int> RequestGenerateAsync(int orderId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>Mark one variation selected for its era (clears the others in that era). 200 / 403 / 404.</summary>
    Task<int> SelectAsync(int portraitId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>One portrait's bytes if it belongs to the caller. StatusCode 200 / 403 / 404.</summary>
    Task<(byte[]? Bytes, string? ContentType, int StatusCode)> GetPortraitBytesAsync(
        int portraitId, string identityId, CancellationToken cancellationToken = default);

    /// <summary>The actual generation loop (called by the Hangfire worker). Bounded, never rethrows — records Status.</summary>
    Task RunGenerationAsync(Guid setId, CancellationToken cancellationToken = default);
}

using Microsoft.AspNetCore.SignalR;

namespace Odin.Api.Hubs
{
    /// <summary>
    /// Pushes a live "a row in the Clients Ancient Origins Results table changed" signal so connected
    /// clients refetch the affected row's full data without a manual reload. Used by every server-side
    /// operation that mutates anything a genetic-inspection row displays (merge status, order status,
    /// qpAdm result, the underlying file, or the row's existence).
    /// </summary>
    public interface IGeneticInspectionRealtimeNotifier
    {
        /// <param name="reason">Informational tag for what changed (e.g. "MergeStatusChanged",
        /// "QpadmResultSubmitted"); the FE reacts the same way regardless — it refetches the table.</param>
        /// <param name="inspectionId">The affected inspection row, when known. Null for file-level events
        /// (e.g. a merge) that may map to more than one row.</param>
        Task NotifyChangedAsync(string reason, int? inspectionId = null, CancellationToken cancellationToken = default);
    }

    public sealed class GeneticInspectionRealtimeNotifier(
        IHubContext<NotificationHub> hubContext,
        ILogger<GeneticInspectionRealtimeNotifier> logger) : IGeneticInspectionRealtimeNotifier
    {
        public async Task NotifyChangedAsync(
            string reason, int? inspectionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Broadcast to all connected (authenticated) clients: this is an internal scientist tool
                // and a change may affect a row owned by any user. The payload is minimal — the FE only
                // needs to know the table changed so it can invalidate + refetch; the id/reason are
                // informational (debugging / future targeted updates).
                await hubContext.Clients.All.SendAsync(
                    "GeneticInspectionsChanged",
                    new { inspectionId, reason },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // A failed live-refresh push must never fail (or roll back) the operation that triggered
                // it; the table still catches up on its next refetch. Log and move on.
                logger.LogWarning(ex,
                    "Failed to broadcast genetic-inspection change ({Reason}, inspection {InspectionId}).",
                    reason, inspectionId);
            }
        }
    }
}

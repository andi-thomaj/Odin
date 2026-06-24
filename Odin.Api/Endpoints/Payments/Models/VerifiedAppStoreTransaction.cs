using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.Payments.Models
{
    /// <summary>
    /// A StoreKit 2 signed transaction that has passed backend validation (signature + chain + bundle id +
    /// product↔service mapping). Carries only the fields the order pipeline needs to record the purchase.
    /// </summary>
    public sealed record VerifiedAppStoreTransaction(
        string TransactionId,
        string OriginalTransactionId,
        string ProductId,
        ServiceType Service,
        DateTime PurchaseDate,
        string Environment,
        string RawJws);

    /// <summary>
    /// Thrown when an App Store transaction fails validation (bad/forged signature, untrusted certificate
    /// chain, wrong bundle id, unknown product, or a product that doesn't match the requested service).
    /// The order endpoints map it to a 400 so the iOS app can surface a clear message and NOT finish the
    /// StoreKit transaction.
    /// </summary>
    public sealed class AppStorePurchaseException(string message) : Exception(message);

    /// <summary>Request body for the App Store Server Notifications V2 webhook (Apple POSTs <c>{ "signedPayload": "…" }</c>).</summary>
    public sealed record AppStoreNotificationRequest(string SignedPayload);

    /// <summary>
    /// A verified + decoded App Store Server Notification V2. <see cref="TransactionId"/> is the affected
    /// purchase (null for notifications that carry no transaction info).
    /// </summary>
    public sealed record AppStoreNotification(
        string NotificationType,
        string? Subtype,
        string? TransactionId,
        string? ProductId,
        string Environment);
}

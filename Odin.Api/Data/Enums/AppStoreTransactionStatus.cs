namespace Odin.Api.Data.Enums
{
    /// <summary>
    /// Lifecycle of a server-recorded Apple StoreKit in-app purchase.
    /// </summary>
    public enum AppStoreTransactionStatus
    {
        /// <summary>Validated by the backend but not yet tied to an order.</summary>
        Verified,

        /// <summary>Consumed to create exactly one paid order (the normal terminal state).</summary>
        Consumed,

        /// <summary>Refunded or revoked by Apple (set by the App Store Server notification webhook).</summary>
        Refunded
    }
}

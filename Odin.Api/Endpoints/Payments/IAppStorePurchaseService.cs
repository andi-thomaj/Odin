using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Endpoints.Payments
{
    /// <summary>
    /// Validates Apple StoreKit 2 in-app purchases for the iOS paid-order flow.
    /// </summary>
    public interface IAppStorePurchaseService
    {
        /// <summary>
        /// Verifies a StoreKit 2 signed transaction JWS and maps it to a normalized result. Verifies the
        /// signature + Apple certificate chain (unless disabled for dev/test), then checks the bundle id,
        /// the environment, and that the purchased product maps to <paramref name="expectedService"/>
        /// (so a cheaper product can't unlock a more expensive service). Throws
        /// <see cref="AppStorePurchaseException"/> on any failure.
        /// </summary>
        VerifiedAppStoreTransaction ValidateTransaction(string signedTransactionJws, ServiceType expectedService);

        /// <summary>
        /// Verifies a StoreKit 2 signed transaction JWS for a NON-order add-on (e.g. the AI ancestral-portraits
        /// product, which isn't a qpAdm/G25 "service"). Same signature + chain + bundle id + environment checks as
        /// <see cref="ValidateTransaction"/>, but matches the payload's <c>productId</c> against
        /// <paramref name="expectedProductId"/> instead of the product↔service map. Throws
        /// <see cref="AppStorePurchaseException"/> on any failure.
        /// </summary>
        VerifiedAddOnTransaction ValidateAddOnTransaction(string signedTransactionJws, string expectedProductId);

        /// <summary>
        /// Verifies and decodes an App Store Server Notification V2 <c>signedPayload</c> (and its nested
        /// <c>signedTransactionInfo</c>). Used by the refund/revoke webhook. Throws
        /// <see cref="AppStorePurchaseException"/> if the payload can't be verified/parsed.
        /// </summary>
        AppStoreNotification ParseNotification(string signedPayload);
    }
}

using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Endpoints.Payments
{
    /// <summary>
    /// Default <see cref="IAppStorePurchaseService"/>: cryptographically verifies the StoreKit 2 signed
    /// transaction against the bundled Apple Root CA - G3, then enforces the business invariants (bundle id,
    /// environment, product↔service). Signature verification is skipped under the <c>Testing</c> host
    /// environment or when <c>AppleIap:VerifySignature=false</c> (local dev / Xcode StoreKit testing, whose
    /// transactions are signed by a local test cert, not Apple's root) — the payload + business checks still run.
    /// </summary>
    public sealed class AppStorePurchaseService : IAppStorePurchaseService
    {
        private readonly AppleIapOptions _options;
        private readonly bool _verifySignature;
        private readonly ILogger<AppStorePurchaseService> _logger;
        private readonly Lazy<X509Certificate2> _appleRoot;

        public AppStorePurchaseService(
            IOptions<AppleIapOptions> options,
            IHostEnvironment hostEnvironment,
            ILogger<AppStorePurchaseService> logger)
        {
            _options = options.Value;
            _logger = logger;
            // Never require Apple's real signing key under integration tests — they craft transactions directly.
            _verifySignature = _options.VerifySignature && !hostEnvironment.IsEnvironment("Testing");
            _appleRoot = new Lazy<X509Certificate2>(LoadAppleRoot);
        }

        public VerifiedAppStoreTransaction ValidateTransaction(string signedTransactionJws, ServiceType expectedService)
        {
            if (string.IsNullOrWhiteSpace(signedTransactionJws))
                throw new AppStorePurchaseException("A purchase token is required to create a paid order.");

            var payloadJson = _verifySignature
                ? AppStoreJwsVerifier.VerifyAndDecode(signedTransactionJws, _appleRoot.Value)
                : AppStoreJwsVerifier.DecodePayload(signedTransactionJws);

            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var bundleId = GetString(root, "bundleId");
            var productId = GetString(root, "productId");
            var transactionId = GetString(root, "transactionId");
            var environment = GetString(root, "environment");

            // Log the decoded fields up-front so a rejected purchase is diagnosable from the server logs
            // (which value tripped which check), regardless of which check below throws.
            _logger.LogInformation(
                "App Store transaction received: transactionId={TransactionId} bundleId={BundleId} " +
                "productId={ProductId} environment={Environment} expectedService={ExpectedService} verifySignature={VerifySignature}",
                transactionId, bundleId, productId, environment, expectedService, _verifySignature);

            if (string.IsNullOrEmpty(transactionId))
                throw new AppStorePurchaseException("Transaction is missing a transaction id.");

            if (!string.Equals(bundleId, _options.BundleId, StringComparison.Ordinal))
                throw new AppStorePurchaseException("Transaction is for a different app.");

            // Only enforce the environment allow-list when we actually verified the signature (production);
            // local/Xcode transactions report environment "Xcode"/"LocalTesting" and are dev-only anyway.
            if (_verifySignature
                && _options.AllowedEnvironments.Length > 0
                && !_options.AllowedEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppStorePurchaseException($"Transaction environment '{environment}' is not accepted.");
            }

            var service = MapProductToService(productId);
            if (service != expectedService)
                throw new AppStorePurchaseException(
                    "The purchased product does not match the requested service.");

            var originalTransactionId = GetString(root, "originalTransactionId");
            if (string.IsNullOrEmpty(originalTransactionId)) originalTransactionId = transactionId;

            var purchaseDate = root.TryGetProperty("purchaseDate", out var pd) && pd.TryGetInt64(out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                : DateTime.UtcNow;

            if (!_verifySignature)
                _logger.LogWarning(
                    "App Store transaction {TransactionId} accepted WITHOUT signature verification (dev/test mode).",
                    transactionId);

            return new VerifiedAppStoreTransaction(
                transactionId, originalTransactionId, productId, service, purchaseDate, environment, signedTransactionJws);
        }

        public VerifiedAddOnTransaction ValidateAddOnTransaction(string signedTransactionJws, string expectedProductId)
        {
            if (string.IsNullOrWhiteSpace(signedTransactionJws))
                throw new AppStorePurchaseException("A purchase token is required to unlock this add-on.");

            var payloadJson = _verifySignature
                ? AppStoreJwsVerifier.VerifyAndDecode(signedTransactionJws, _appleRoot.Value)
                : AppStoreJwsVerifier.DecodePayload(signedTransactionJws);

            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var bundleId = GetString(root, "bundleId");
            var productId = GetString(root, "productId");
            var transactionId = GetString(root, "transactionId");
            var environment = GetString(root, "environment");

            _logger.LogInformation(
                "App Store add-on transaction received: transactionId={TransactionId} bundleId={BundleId} " +
                "productId={ProductId} environment={Environment} expectedProductId={ExpectedProductId} verifySignature={VerifySignature}",
                transactionId, bundleId, productId, environment, expectedProductId, _verifySignature);

            if (string.IsNullOrEmpty(transactionId))
                throw new AppStorePurchaseException("Transaction is missing a transaction id.");

            if (!string.Equals(bundleId, _options.BundleId, StringComparison.Ordinal))
                throw new AppStorePurchaseException("Transaction is for a different app.");

            if (_verifySignature
                && _options.AllowedEnvironments.Length > 0
                && !_options.AllowedEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppStorePurchaseException($"Transaction environment '{environment}' is not accepted.");
            }

            if (!string.Equals(productId, expectedProductId, StringComparison.Ordinal))
                throw new AppStorePurchaseException("The purchased product does not match this add-on.");

            var originalTransactionId = GetString(root, "originalTransactionId");
            if (string.IsNullOrEmpty(originalTransactionId)) originalTransactionId = transactionId;

            var purchaseDate = root.TryGetProperty("purchaseDate", out var pd) && pd.TryGetInt64(out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                : DateTime.UtcNow;

            if (!_verifySignature)
                _logger.LogWarning(
                    "App Store add-on transaction {TransactionId} accepted WITHOUT signature verification (dev/test mode).",
                    transactionId);

            return new VerifiedAddOnTransaction(
                transactionId, originalTransactionId, productId, purchaseDate, environment, signedTransactionJws);
        }

        public AppStoreNotification ParseNotification(string signedPayload)
        {
            if (string.IsNullOrWhiteSpace(signedPayload))
                throw new AppStorePurchaseException("Notification payload is empty.");

            var payloadJson = _verifySignature
                ? AppStoreJwsVerifier.VerifyAndDecode(signedPayload, _appleRoot.Value)
                : AppStoreJwsVerifier.DecodePayload(signedPayload);

            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var notificationType = GetString(root, "notificationType");
            var subtype = GetString(root, "subtype");
            string? transactionId = null;
            string? productId = null;
            var environment = string.Empty;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                environment = GetString(data, "environment");

                // The affected transaction is itself a nested signed JWS — verify + decode it too.
                if (data.TryGetProperty("signedTransactionInfo", out var sti)
                    && sti.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(sti.GetString()))
                {
                    var txJson = _verifySignature
                        ? AppStoreJwsVerifier.VerifyAndDecode(sti.GetString()!, _appleRoot.Value)
                        : AppStoreJwsVerifier.DecodePayload(sti.GetString()!);
                    using var txDoc = JsonDocument.Parse(txJson);
                    transactionId = GetString(txDoc.RootElement, "transactionId");
                    productId = GetString(txDoc.RootElement, "productId");
                }
            }

            return new AppStoreNotification(
                notificationType, string.IsNullOrEmpty(subtype) ? null : subtype, transactionId, productId, environment);
        }

        private ServiceType MapProductToService(string productId)
        {
            if (string.Equals(productId, _options.QpadmProductId, StringComparison.Ordinal))
                return ServiceType.qpAdm;
            if (string.Equals(productId, _options.G25ProductId, StringComparison.Ordinal))
                return ServiceType.g25;
            throw new AppStorePurchaseException($"Unknown product '{productId}'.");
        }

        private X509Certificate2 LoadAppleRoot()
        {
            // An explicit path wins (e.g. a rotated root mounted at deploy time without a rebuild).
            if (!string.IsNullOrWhiteSpace(_options.AppleRootCertPath))
                return X509CertificateLoader.LoadCertificateFromFile(_options.AppleRootCertPath);

            // Otherwise use the Apple Root CA - G3 bundled with the API (embedded resource), so production
            // works with no file to deploy — just AppleIap:VerifySignature=true (the default).
            const string resourceName = "Odin.Api.Certificates.AppleRootCA-G3.cer";
            var assembly = typeof(AppStorePurchaseService).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new AppStorePurchaseException(
                    $"Bundled Apple root certificate '{resourceName}' was not found and AppleIap:AppleRootCertPath " +
                    "is not set. Set the path, or AppleIap:VerifySignature=false for local/Xcode StoreKit testing.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return X509CertificateLoader.LoadCertificate(memory.ToArray());
        }

        private static string GetString(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }
}

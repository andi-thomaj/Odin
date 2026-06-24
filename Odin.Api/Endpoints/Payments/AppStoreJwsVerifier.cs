using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Endpoints.Payments
{
    /// <summary>
    /// Verifies and decodes Apple StoreKit 2 / App Store Server JWS payloads. A JWS is
    /// <c>base64url(header).base64url(payload).base64url(signature)</c> signed with ES256; the signing
    /// certificate chain (leaf → Apple intermediate → Apple root) rides in the header's <c>x5c</c> array.
    /// We trust the bundled Apple Root CA - G3 as a custom anchor and verify the ES256 signature over
    /// <c>header.payload</c> with the leaf cert's public key. Shared by the purchase validator and the
    /// App Store Server notification webhook.
    /// </summary>
    internal static class AppStoreJwsVerifier
    {
        /// <summary>Decodes the JWS payload to its JSON string WITHOUT verifying the signature.</summary>
        public static string DecodePayload(string jws)
        {
            var parts = jws.Split('.');
            if (parts.Length != 3)
                throw new AppStorePurchaseException("Malformed transaction token.");
            return Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        }

        /// <summary>
        /// Verifies the JWS certificate chain against <paramref name="appleRoot"/> and the ES256 signature.
        /// Returns the decoded payload JSON string. Throws <see cref="AppStorePurchaseException"/> on any failure.
        /// </summary>
        public static string VerifyAndDecode(string jws, X509Certificate2 appleRoot)
        {
            var parts = jws.Split('.');
            if (parts.Length != 3)
                throw new AppStorePurchaseException("Malformed transaction token.");

            // ── Header → x5c certificate chain ────────────────────────────────────────────────
            using var headerDoc = JsonDocument.Parse(Base64UrlDecode(parts[0]));
            if (!headerDoc.RootElement.TryGetProperty("x5c", out var x5c) || x5c.GetArrayLength() == 0)
                throw new AppStorePurchaseException("Transaction token is missing its signing certificate chain.");

            var certs = new List<X509Certificate2>();
            try
            {
                foreach (var entry in x5c.EnumerateArray())
                {
                    // x5c entries are STANDARD base64 (not base64url) DER certificates.
                    var der = Convert.FromBase64String(entry.GetString() ?? string.Empty);
                    certs.Add(X509CertificateLoader.LoadCertificate(der));
                }

                var leaf = certs[0];

                // ── Chain: leaf must chain to OUR trusted Apple root (not the one embedded in the token) ──
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(appleRoot);
                for (var i = 1; i < certs.Count; i++)
                    chain.ChainPolicy.ExtraStore.Add(certs[i]);

                if (!chain.Build(leaf))
                {
                    var status = string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation.Trim()));
                    throw new AppStorePurchaseException(
                        $"Transaction certificate chain is not trusted by the Apple root. {status}".Trim());
                }

                // ── Signature: ES256 over ASCII(header.payload); JWS encodes the signature as raw r||s ──
                using var ecdsa = leaf.GetECDsaPublicKey()
                    ?? throw new AppStorePurchaseException("Transaction signing certificate is not an ECDSA key.");
                var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
                var signature = Base64UrlDecode(parts[2]);
                if (!ecdsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256))
                    throw new AppStorePurchaseException("Transaction signature is invalid.");

                return Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            }
            finally
            {
                foreach (var cert in certs)
                    cert.Dispose();
            }
        }

        private static byte[] Base64UrlDecode(string value)
        {
            var s = value.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}

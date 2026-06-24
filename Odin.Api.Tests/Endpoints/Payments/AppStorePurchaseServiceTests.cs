using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Payments;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Tests.Endpoints.Payments;

/// <summary>
/// Unit tests for the App Store purchase validator's payload + business checks. Signature verification is
/// disabled here (<c>VerifySignature=false</c>), exactly as it is under the Testing/dev environments — so
/// these assert the bundle id, product↔service mapping, and error handling, not the cryptography.
/// </summary>
public class AppStorePurchaseServiceTests
{
    private static AppStorePurchaseService CreateService() =>
        new(Options.Create(new AppleIapOptions { VerifySignature = false }),
            new FakeHostEnvironment(),
            NullLogger<AppStorePurchaseService>.Instance);

    [Fact]
    public void ValidateTransaction_Qpadm_ReturnsVerified()
    {
        var service = CreateService();
        var jws = BuildJws("io.ancestrify.app", "io.ancestrify.app.qpadm", "txn-123");

        var result = service.ValidateTransaction(jws, ServiceType.qpAdm);

        Assert.Equal(ServiceType.qpAdm, result.Service);
        Assert.Equal("txn-123", result.TransactionId);
        Assert.Equal("io.ancestrify.app.qpadm", result.ProductId);
    }

    [Fact]
    public void ValidateTransaction_G25_ReturnsVerified()
    {
        var service = CreateService();
        var jws = BuildJws("io.ancestrify.app", "io.ancestrify.app.g25", "txn-g25");

        var result = service.ValidateTransaction(jws, ServiceType.g25);

        Assert.Equal(ServiceType.g25, result.Service);
    }

    [Fact]
    public void ValidateTransaction_ProductServiceMismatch_Throws()
    {
        var service = CreateService();
        // A G25 product cannot be redeemed for a (more expensive) qpAdm order.
        var jws = BuildJws("io.ancestrify.app", "io.ancestrify.app.g25", "txn-x");

        Assert.Throws<AppStorePurchaseException>(() => service.ValidateTransaction(jws, ServiceType.qpAdm));
    }

    [Fact]
    public void ValidateTransaction_WrongBundleId_Throws()
    {
        var service = CreateService();
        var jws = BuildJws("com.someone.else", "io.ancestrify.app.qpadm", "txn-x");

        Assert.Throws<AppStorePurchaseException>(() => service.ValidateTransaction(jws, ServiceType.qpAdm));
    }

    [Fact]
    public void ValidateTransaction_UnknownProduct_Throws()
    {
        var service = CreateService();
        var jws = BuildJws("io.ancestrify.app", "io.ancestrify.app.unknown", "txn-x");

        Assert.Throws<AppStorePurchaseException>(() => service.ValidateTransaction(jws, ServiceType.qpAdm));
    }

    [Fact]
    public void ValidateTransaction_EmptyToken_Throws()
    {
        var service = CreateService();
        Assert.Throws<AppStorePurchaseException>(() => service.ValidateTransaction("", ServiceType.qpAdm));
    }

    [Fact]
    public void AppleRootCertificate_IsBundledAsEmbeddedResource()
    {
        // Production verification relies on this cert shipping inside the API assembly. Guard against it
        // being renamed/dropped from the csproj.
        var assembly = typeof(AppStorePurchaseService).Assembly;
        using var stream = assembly.GetManifestResourceStream("Odin.Api.Certificates.AppleRootCA-G3.cer");
        Assert.NotNull(stream);

        using var memory = new MemoryStream();
        stream!.CopyTo(memory);
        using var cert = X509CertificateLoader.LoadCertificate(memory.ToArray());
        Assert.Contains("Apple Root CA - G3", cert.Subject);
    }

    private static string BuildJws(string bundleId, string productId, string transactionId)
    {
        var header = Base64Url("{\"alg\":\"ES256\"}"u8.ToArray());
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            bundleId,
            productId,
            transactionId,
            originalTransactionId = transactionId,
            purchaseDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            type = "Consumable",
            environment = "Xcode",
        });
        var payload = Base64Url(System.Text.Encoding.UTF8.GetBytes(payloadJson));
        var signature = Base64Url("sig"u8.ToArray());
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Odin.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

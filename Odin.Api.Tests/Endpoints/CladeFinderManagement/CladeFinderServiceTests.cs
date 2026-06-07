using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Endpoints.CladeFinderManagement;

namespace Odin.Api.Tests.Endpoints.CladeFinderManagement;

public class CladeFinderServiceTests
{
    private const string SuccessJson = """
        {
          "clade": "A-B-C",
          "score": 6.0,
          "next_prediction": { "clade": "A-B", "score": 4.0 },
          "downstream": [],
          "warning": null,
          "error": null,
          "positives_used": 6,
          "negatives_used": 3,
          "y_reads": 9,
          "source_format": "microarray"
        }
        """;

    [Fact]
    public async Task AnalyzeAsync_PostsMultipartWithApiKey_AndMapsSnakeCaseResponse()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new StubHandler(async (req, _) =>
        {
            captured = req;
            body = req.Content is not null ? await req.Content.ReadAsStringAsync() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessJson, System.Text.Encoding.UTF8, "application/json"),
            };
        });

        var service = CreateService(handler, apiKey: "s3cret");
        var file = MakeFormFile("rs1\tY\t1001\tT\n"u8.ToArray(), "sample.txt");

        var result = await service.AnalyzeAsync(file, "hg38");

        Assert.Equal("A-B-C", result.Clade);
        Assert.Equal(6.0, result.Score);
        Assert.NotNull(result.NextPrediction);
        Assert.Equal("A-B", result.NextPrediction!.Clade);
        Assert.Equal(4.0, result.NextPrediction.Score);
        Assert.Equal(6, result.PositivesUsed);
        Assert.Equal(3, result.NegativesUsed);
        Assert.Equal(9, result.YReads);
        Assert.Equal("microarray", result.SourceFormat);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.EndsWith("/v1/clade-finder/analyze", captured.RequestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.True(captured.Headers.TryGetValues("X-Api-Key", out var keys));
        Assert.Equal("s3cret", keys!.Single());
        Assert.NotNull(body);
        Assert.Contains("filename=sample.txt", body!, StringComparison.Ordinal);
        Assert.Contains("hg38", body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsync_OmitsApiKeyHeader_WhenNotConfigured()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SuccessJson, System.Text.Encoding.UTF8, "application/json"),
            });
        });

        var service = CreateService(handler, apiKey: "");
        await service.AnalyzeAsync(MakeFormFile("rs1\tY\t1\tT\n"u8.ToArray(), "x.txt"), build: null);

        Assert.False(captured!.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task AnalyzeAsync_Throws_WithStatusAndDetail_OnErrorResponse()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"detail":"reference data not configured"}"""),
            }));

        var service = CreateService(handler, apiKey: "k");

        var ex = await Assert.ThrowsAsync<CladeFinderException>(() =>
            service.AnalyzeAsync(MakeFormFile("d"u8.ToArray(), "x.txt"), null));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal("reference data not configured", ex.Detail);
    }

    [Fact]
    public async Task AnalyzeAsync_Throws_WhenBaseUrlNotConfigured()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        // No BaseAddress / BaseUrl configured.
        using var client = new HttpClient(handler);
        var options = Options.Create(new ToolsApiOptions { BaseUrl = "", ApiKey = "" });
        var service = new CladeFinderService(client, options, NullLogger<CladeFinderService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnalyzeAsync(MakeFormFile("d"u8.ToArray(), "x.txt"), null));
    }

    private static CladeFinderService CreateService(HttpMessageHandler handler, string apiKey)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://tools.local") };
        var options = Options.Create(new ToolsApiOptions
        {
            BaseUrl = "http://tools.local",
            ApiKey = apiKey,
            TimeoutSeconds = 30,
        });
        return new CladeFinderService(client, options, NullLogger<CladeFinderService>.Instance);
    }

    private static IFormFile MakeFormFile(byte[] content, string fileName)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain",
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}

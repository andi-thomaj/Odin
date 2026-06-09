using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Tests.Endpoints.MergeManagement;

public class MergePipelineServiceTests
{
    // Exactly the snake_case shape odin-tools-api's ConvertResponse emits. Note "converted_23andme":
    // the .NET SnakeCaseLower policy would map the property Converted23Andme to "converted23_andme",
    // so this field only binds because of the explicit [JsonPropertyName]. This is the regression
    // guard for the merge job's Encoding.GetBytes(null) crash.
    private const string ConvertSuccessJson = """
        {
          "converted_23andme": "rs1\t1\t1001\tAA\n",
          "file_name": "converted.txt",
          "source_vendor": "ancestry"
        }
        """;

    [Fact]
    public async Task ConvertAsync_BindsConverted23Andme_FromSnakeCaseResponse()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ConvertSuccessJson, System.Text.Encoding.UTF8, "application/json"),
            }));

        var service = CreateService(handler, apiKey: "k");

        var result = await service.ConvertAsync("rawbytes"u8.ToArray(), "upload.txt");

        // The crux: this used to come back null (field name didn't bind), which then blew up the
        // merge job at Encoding.UTF8.GetBytes(converted.Converted23Andme).
        Assert.NotNull(result.Converted23Andme);
        Assert.Equal("rs1\t1\t1001\tAA\n", result.Converted23Andme);
        Assert.Equal("converted.txt", result.FileName);
        Assert.Equal("ancestry", result.SourceVendor);
    }

    [Fact]
    public async Task ConvertAsync_PostsMultipartWithApiKey_ToConvertEndpoint()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new StubHandler(async (req, _) =>
        {
            captured = req;
            body = req.Content is not null ? await req.Content.ReadAsStringAsync() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ConvertSuccessJson, System.Text.Encoding.UTF8, "application/json"),
            };
        });

        var service = CreateService(handler, apiKey: "s3cret");
        await service.ConvertAsync("rawbytes"u8.ToArray(), "sample.txt");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.EndsWith("/v1/merge/convert", captured.RequestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.True(captured.Headers.TryGetValues("X-Api-Key", out var keys));
        Assert.Equal("s3cret", keys!.Single());
        Assert.NotNull(body);
        Assert.Contains("filename=sample.txt", body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunMergeAsync_MapsSnakeCaseResponse()
    {
        const string mergeJson = """
            {
              "merge_id": "insp-1-abc",
              "file_name": "insp-1-abc.tar.gz",
              "size_bytes": 4096,
              "panel": "HO"
            }
            """;
        HttpRequestMessage? captured = null;
        var handler = new StubHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(mergeJson, System.Text.Encoding.UTF8, "application/json"),
            });
        });

        var service = CreateService(handler, apiKey: "k");
        var result = await service.RunMergeAsync("insp-1-abc", "rs1\t1\t1\tAA\n", panel: null, sampleId: "S1", sex: "1");

        Assert.Equal("insp-1-abc", result.MergeId);
        Assert.Equal("insp-1-abc.tar.gz", result.FileName);
        Assert.Equal(4096, result.SizeBytes);
        Assert.Equal("HO", result.Panel);
        Assert.EndsWith("/v1/merge/run", captured!.RequestUri?.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_Throws_WithStatusAndDetail_OnErrorResponse()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"detail":"unsupported vendor format"}"""),
            }));

        var service = CreateService(handler, apiKey: "k");

        var ex = await Assert.ThrowsAsync<MergePipelineException>(() =>
            service.ConvertAsync("d"u8.ToArray(), "x.txt"));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("unsupported vendor format", ex.Detail);
    }

    [Fact]
    public async Task ConvertAsync_Throws_WhenBaseUrlNotConfigured()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);
        var options = Options.Create(new ToolsApiOptions { BaseUrl = "", ApiKey = "" });
        var service = new MergePipelineService(client, options, NullLogger<MergePipelineService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConvertAsync("d"u8.ToArray(), "x.txt"));
    }

    private static MergePipelineService CreateService(HttpMessageHandler handler, string apiKey)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://tools.local") };
        var options = Options.Create(new ToolsApiOptions
        {
            BaseUrl = "http://tools.local",
            ApiKey = apiKey,
            TimeoutSeconds = 30,
        });
        return new MergePipelineService(client, options, NullLogger<MergePipelineService>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}

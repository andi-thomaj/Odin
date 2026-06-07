using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Odin.Api.Endpoints.CladeFinderManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.CladeFinderManagement;

public class CladeFinderEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static MultipartFormDataContent BuildForm(byte[] content, string fileName, string? build = null)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(part, "file", fileName);
        if (build is not null)
        {
            form.Add(new StringContent(build), "build");
        }

        return form;
    }

    private static byte[] SampleData => "rs1\tY\t1001\tT\n"u8.ToArray();

    [Fact]
    public async Task Analyze_ValidFile_ReturnsMappedClade()
    {
        using var form = BuildForm(SampleData, "genome.txt", "hg38");

        var response = await Client.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AnalyzeCladeContract.Response>();
        Assert.NotNull(body);
        Assert.Equal("A-B-C", body!.Clade);
        Assert.Equal("A-B", body.NextPrediction?.Clade);
        Assert.Equal(6, body.PositivesUsed);
        Assert.Equal("microarray", body.SourceFormat);
    }

    [Fact]
    public async Task Analyze_VcfFile_ReportsVcfSourceFormat()
    {
        using var form = BuildForm("##fileformat=VCFv4.2\n"u8.ToArray(), "genome.vcf");

        var response = await Client.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AnalyzeCladeContract.Response>();
        Assert.Equal("vcf", body!.SourceFormat);
    }

    [Fact]
    public async Task Analyze_InvalidExtension_ReturnsBadRequest()
    {
        using var form = BuildForm(SampleData, "genome.exe");

        var response = await Client.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_EmptyFile_ReturnsBadRequest()
    {
        using var form = BuildForm([], "genome.txt");

        var response = await Client.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_WhenToolsApiUnavailable_Returns503()
    {
        using var form = BuildForm(SampleData, "boom-503.txt");

        var response = await Client.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_WhenToolsApiRejectsInput_Returns400()
    {
        using var form = BuildForm(SampleData, "boom-400.txt");

        var response = await Client.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_Unauthenticated_ReturnsUnauthorized()
    {
        var anonymous = Factory.CreateDefaultClient(new ApiVersionPrefixHandler());
        anonymous.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Unauthenticated", "true");
        using var form = BuildForm(SampleData, "genome.txt");

        var response = await anonymous.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_AsUserRole_IsAllowed()
    {
        // Endpoint requires only EmailVerified (not Scientist/Admin), so a plain user is permitted.
        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");
        using var form = BuildForm(SampleData, "genome.txt");

        var response = await userClient.PostAsync("/api/clade-finder/analyze", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

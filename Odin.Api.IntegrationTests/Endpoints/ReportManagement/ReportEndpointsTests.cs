using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.ReportManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.ReportManagement;

public class ReportEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/reports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<ReportListContract.ListItem>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task Create_WithValidForm_ReturnsCreated()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Bug"), "type");
        content.Add(new StringContent("Test subject"), "subject");
        content.Add(new StringContent("Test description body."), "description");

        var response = await Client.PostAsync("/api/reports", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateReportContract.Response>();
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal("Bug", body.Type);
        Assert.Equal("Test subject", body.Subject);
    }
}

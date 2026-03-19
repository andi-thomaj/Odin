using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.NotificationManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.NotificationManagement;

public class NotificationEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/notifications/unread-count");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UnreadCountContract.Response>();
        Assert.NotNull(body);
        Assert.True(body.Count >= 0);
    }
}

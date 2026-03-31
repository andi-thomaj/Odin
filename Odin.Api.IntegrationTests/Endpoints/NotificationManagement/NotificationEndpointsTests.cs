using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.NotificationManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.NotificationManagement;

public class NotificationEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── GET /api/notifications ─────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task GetAll_AfterSeeding_ReturnsNotifications()
    {
        var userId = await ResolveUserIdAsync(Factory.Services, "auth0|integration-default");
        await SeedNotificationsAsync(Factory.Services, userId, 3);

        var response = await Client.GetAsync("/api/notifications");
        var list = await response.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();

        Assert.NotNull(list);
        Assert.Equal(3, list!.Count);
    }

    [Fact]
    public async Task GetAll_Pagination_RespectsPageSize()
    {
        var userId = await ResolveUserIdAsync(Factory.Services, "auth0|integration-default");
        await SeedNotificationsAsync(Factory.Services, userId, 5);

        var response = await Client.GetAsync("/api/notifications?page=1&pageSize=2");
        var list = await response.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task GetAll_OrderedByCreatedAtDescending()
    {
        var userId = await ResolveUserIdAsync(Factory.Services, "auth0|integration-default");
        await SeedNotificationsAsync(Factory.Services, userId, 3);

        var response = await Client.GetAsync("/api/notifications");
        var list = await response.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();

        Assert.NotNull(list);
        for (var i = 0; i < list!.Count - 1; i++)
            Assert.True(list[i].CreatedAt >= list[i + 1].CreatedAt);
    }

    // ── GET /api/notifications/unread-count ────────────────────────

    [Fact]
    public async Task GetUnreadCount_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/notifications/unread-count");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUnreadCount_AfterSeeding_ReturnsCorrectCount()
    {
        var userId = await ResolveUserIdAsync(Factory.Services, "auth0|integration-default");
        await SeedNotificationsAsync(Factory.Services, userId, 4);

        var response = await Client.GetAsync("/api/notifications/unread-count");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("4", json);
    }

    // ── PATCH /api/notifications/read-status ───────────────────────

    [Fact]
    public async Task MarkAllAsRead_ReturnsNoContent()
    {
        var userId = await ResolveUserIdAsync(Factory.Services, "auth0|integration-default");
        await SeedNotificationsAsync(Factory.Services, userId, 3);

        var response = await Client.PatchAsync("/api/notifications/read-status", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MarkAllAsRead_SetsUnreadCountToZero()
    {
        var userId = await ResolveUserIdAsync(Factory.Services, "auth0|integration-default");
        await SeedNotificationsAsync(Factory.Services, userId, 3);

        await Client.PatchAsync("/api/notifications/read-status", null);

        var countResponse = await Client.GetAsync("/api/notifications/unread-count");
        var json = await countResponse.Content.ReadAsStringAsync();
        Assert.Contains("0", json);
    }

    [Fact]
    public async Task MarkAllAsRead_NotificationsShowAsRead()
    {
        var userId = await ResolveUserIdAsync(Factory.Services, "auth0|integration-default");
        await SeedNotificationsAsync(Factory.Services, userId, 2);

        await Client.PatchAsync("/api/notifications/read-status", null);

        var response = await Client.GetAsync("/api/notifications");
        var list = await response.Content.ReadFromJsonAsync<List<GetNotificationContract.Response>>();

        Assert.NotNull(list);
        Assert.All(list!, n =>
        {
            Assert.True(n.IsRead);
            Assert.NotNull(n.ReadAt);
        });
    }

    // ── Authorization ──────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = CreateUnauthenticatedClient(Factory);

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

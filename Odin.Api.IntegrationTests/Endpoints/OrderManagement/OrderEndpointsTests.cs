using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.OrderManagement;

public class OrderEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<GetOrderContract.Response>>();
        Assert.NotNull(orders);
    }
}

using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25AncientManagement;
using Odin.Api.Endpoints.G25AncientManagement.Models;

namespace Odin.Api.Tests.G25AncientManagement;

public class G25AncientServiceTests
{
    [Fact]
    public async Task GetPagedAsync_ReturnsPageAndTotal()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            db.G25Ancients.Add(new G25Ancient
            {
                Label = $"L{i}",
                Coordinates = $"c{i}",
                CreatedAt = now,
                CreatedBy = "t",
                UpdatedAt = now,
                UpdatedBy = "t"
            });
        }

        await db.SaveChangesAsync();

        var service = new G25AncientService(db);
        var p1 = await service.GetPagedAsync(1, 2);

        Assert.Equal(5, p1.TotalCount);
        Assert.Equal(1, p1.Page);
        Assert.Equal(2, p1.PageSize);
        Assert.Equal(2, p1.Items.Count);
        Assert.Equal("L0", p1.Items[0].Label);
    }

    [Fact]
    public async Task CreateUpdateDelete_RoundTrips()
    {
        await using var db = CreateDbContext();
        var service = new G25AncientService(db);

        var created = await service.CreateAsync("user1", new CreateG25AncientContract.Request
        {
            Label = "Denisova25",
            Coordinates = "sample,1,2"
        });

        Assert.True(created.Id > 0);

        var byId = await service.GetByIdAsync(created.Id);
        Assert.NotNull(byId);
        Assert.Equal("Denisova25", byId!.Label);

        var updated = await service.UpdateAsync(created.Id, "user1", new UpdateG25AncientContract.Request
        {
            Label = "Denisova25b",
            Coordinates = "sample,2,3"
        });
        Assert.NotNull(updated);
        Assert.Equal("Denisova25b", updated!.Label);

        var ok = await service.DeleteAsync(created.Id);
        Assert.True(ok);
        Assert.Null(await service.GetByIdAsync(created.Id));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"g25-tests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}

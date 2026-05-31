using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data.Entities;
using Odin.Api.IntegrationTests.Infrastructure;
using Odin.Api.Services;

namespace Odin.Api.IntegrationTests.Logging;

// Verifies the recurring-job cleanup logic registered with Hangfire in Program.cs.
// Hangfire itself isn't started in the Testing environment (see Program.cs gate),
// so these tests invoke the service directly — the same way the Hangfire worker would.
public class LogCleanupServiceTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task DeleteAllLogsAsync_RemovesAllRows_AndReturnsCount()
    {
        await using (var db = await GetDbContextAsync())
        {
            db.Logs.AddRange(
                MakeLog("seed-1", "Error"),
                MakeLog("seed-2", "Error"),
                MakeLog("seed-3", "Fatal"),
                MakeLog("seed-4", "Information"));
            await db.SaveChangesAsync();
        }

        int deleted;
        using (var scope = Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ILogCleanupService>();
            deleted = await service.DeleteAllLogsAsync();
        }

        Assert.Equal(4, deleted);

        await using var verifyDb = await GetDbContextAsync();
        var remaining = await verifyDb.Logs.AsNoTracking().CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task DeleteAllLogsAsync_OnEmptyTable_ReturnsZero()
    {
        await using (var db = await GetDbContextAsync())
        {
            Assert.Equal(0, await db.Logs.CountAsync());
        }

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogCleanupService>();

        var deleted = await service.DeleteAllLogsAsync();

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteAllLogsAsync_DoesNotTouchOtherTables()
    {
        // Sanity check: the cleanup must scope to the logs table only. Use the seeded
        // integration user from IntegrationTestBase as a witness row in another table.
        int usersBefore;
        await using (var db = await GetDbContextAsync())
        {
            db.Logs.Add(MakeLog("scoped-1", "Error"));
            await db.SaveChangesAsync();
            usersBefore = await db.Users.CountAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ILogCleanupService>();
            await service.DeleteAllLogsAsync();
        }

        await using var verifyDb = await GetDbContextAsync();
        Assert.Equal(0, await verifyDb.Logs.AsNoTracking().CountAsync());
        Assert.Equal(usersBefore, await verifyDb.Users.AsNoTracking().CountAsync());
    }

    private static Log MakeLog(string marker, string level) => new()
    {
        Message = $"seeded log {marker}",
        MessageTemplate = "seeded log {Marker}",
        Level = level,
        Timestamp = DateTime.UtcNow,
        Exception = level is "Error" or "Fatal" ? $"System.InvalidOperationException: {marker}" : null,
        Properties = $"{{\"Marker\":\"{marker}\"}}"
    };
}

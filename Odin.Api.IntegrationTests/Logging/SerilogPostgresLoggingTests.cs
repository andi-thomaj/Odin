using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Logging;

// Verifies the Serilog → Postgres pipeline: every Error+ event reaches the `logs` table.
// Regression coverage for the "logs table stays empty" bug — the Serilog Postgres sink
// requires `respectCase: true` so the COPY statement quotes the PascalCase column names
// the EF migration created.
public class SerilogPostgresLoggingTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UnhandledException_FromEndpoint_PersistsErrorRowInLogsTable()
    {
        var marker = $"throw-{Guid.NewGuid():N}";

        var response = await Client.GetAsync($"/api/diagnostics/throw?marker={marker}");

        // GlobalExceptionHandlerMiddleware maps InvalidOperationException → 400.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var row = await WaitForLogRowAsync(l => l.Message.Contains(marker) || (l.Exception != null && l.Exception.Contains(marker)));

        Assert.NotNull(row);
        Assert.Equal("Error", row!.Level);
        Assert.NotNull(row.Exception);
        Assert.Contains(nameof(InvalidOperationException), row.Exception);
        Assert.Contains(marker, row.Exception);
        Assert.False(string.IsNullOrWhiteSpace(row.Message));
    }

    [Fact]
    public async Task DirectLoggerError_PersistsRowInLogsTable()
    {
        var marker = $"logger-{Guid.NewGuid():N}";

        using (var scope = Factory.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SerilogPostgresLoggingTests>>();
            logger.LogError(
                new InvalidOperationException($"Direct logger error {marker}"),
                "Direct logger error with marker {Marker}",
                marker);
        }

        var row = await WaitForLogRowAsync(l => l.Message.Contains(marker) || (l.Exception != null && l.Exception.Contains(marker)));

        Assert.NotNull(row);
        Assert.Equal("Error", row!.Level);
        Assert.NotNull(row.Exception);
        Assert.Contains(marker, row.Exception);
    }

    [Fact]
    public async Task DirectLoggerCritical_PersistsRowInLogsTable()
    {
        var marker = $"critical-{Guid.NewGuid():N}";

        using (var scope = Factory.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SerilogPostgresLoggingTests>>();
            logger.LogCritical("Critical event with marker {Marker}", marker);
        }

        var row = await WaitForLogRowAsync(l => l.Message.Contains(marker));

        Assert.NotNull(row);
        Assert.Equal("Fatal", row!.Level); // Microsoft.Extensions.Logging.LogLevel.Critical → Serilog LogEventLevel.Fatal
        Assert.Contains(marker, row.Message);
    }

    [Fact]
    public async Task DirectLoggerInformation_DoesNotPersistRowInLogsTable()
    {
        var marker = $"info-{Guid.NewGuid():N}";

        using (var scope = Factory.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SerilogPostgresLoggingTests>>();
            logger.LogInformation("Information event with marker {Marker}", marker);
            logger.LogWarning("Warning event with marker {Marker}", marker);
        }

        // Send an Error event AFTER the Info/Warning so the batch is definitely flushed.
        var sentinelMarker = $"sentinel-{Guid.NewGuid():N}";
        using (var scope = Factory.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SerilogPostgresLoggingTests>>();
            logger.LogError("Sentinel error after info/warning {Marker}", sentinelMarker);
        }

        // Wait for the sentinel — once it lands, the batch with the earlier Info/Warning has been processed.
        var sentinel = await WaitForLogRowAsync(l => l.Message.Contains(sentinelMarker));
        Assert.NotNull(sentinel);

        // Now confirm no Info/Warning row exists with the original marker.
        await using var db = await GetDbContextAsync();
        var unwantedRows = await db.Logs
            .AsNoTracking()
            .Where(l => l.Message.Contains(marker))
            .ToListAsync();

        Assert.Empty(unwantedRows);
    }

    [Fact]
    public async Task LogRow_PopulatesCoreColumns()
    {
        var marker = $"columns-{Guid.NewGuid():N}";
        // Serilog's Postgres sink stores the event timestamp as local wall-clock time, so bound the
        // freshness window with DateTime.Now (not UtcNow) — comparing a local timestamp against UtcNow
        // fails on any non-UTC machine while passing in CI (UTC). On a UTC host the two are identical.
        var before = DateTime.Now.AddSeconds(-5);

        using (var scope = Factory.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SerilogPostgresLoggingTests>>();
            logger.LogError(
                new InvalidOperationException("boom"),
                "Marker {Marker} extra {Extra}",
                marker,
                "value");
        }

        var row = await WaitForLogRowAsync(l => l.Message.Contains(marker));

        Assert.NotNull(row);
        Assert.NotEqual(0, row!.Id);
        Assert.False(string.IsNullOrWhiteSpace(row.Message));
        Assert.Contains(marker, row.Message);
        Assert.False(string.IsNullOrWhiteSpace(row.MessageTemplate));
        Assert.Equal("Error", row.Level);
        Assert.True(row.Timestamp >= before, $"Timestamp {row.Timestamp:o} should be >= {before:o}");
        Assert.True(row.Timestamp <= DateTime.Now.AddSeconds(5));
        Assert.NotNull(row.Exception);
        Assert.NotNull(row.Properties);
        Assert.Contains("Marker", row.Properties);
    }

    // Serilog's Postgres sink is a periodic-batching sink. The Testing-env config in Program.cs
    // uses a 50ms period + batch size 1, so a single event normally lands within a few hundred ms.
    // We still poll up to 10 seconds to absorb container/CI jitter.
    private async Task<Log?> WaitForLogRowAsync(System.Linq.Expressions.Expression<Func<Log, bool>> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = await GetDbContextAsync();
            var row = await db.Logs
                .AsNoTracking()
                .Where(predicate)
                .OrderByDescending(l => l.Id)
                .FirstOrDefaultAsync();
            if (row is not null)
                return row;
            await Task.Delay(100);
        }
        return null;
    }
}

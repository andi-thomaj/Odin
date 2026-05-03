using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Odin.Api.Data;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace Odin.Api.Tests.Logging;

public class SerilogPostgresLogTests
{
    [Fact]
    public async Task Error_From_ThrownException_Is_Persisted_To_Logs_Table()
    {
        var connectionString = ResolveConnectionString();
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "ConnectionStrings:DefaultConnection must be set in Odin.Api user-secrets " +
            "or via the ConnectionStrings__DefaultConnection environment variable.");

        var marker = $"odin-test-{Guid.NewGuid():N}";

        // Same column writers as Program.cs. `respectCase: true` is required so the COPY
        // statement quotes the PascalCase column names — without it, Postgres folds them
        // to lowercase and rejects the insert because the real columns are "Message",
        // "Level", etc.
        var columnWriters = new Dictionary<string, ColumnWriterBase>
        {
            { "Message", new RenderedMessageColumnWriter() },
            { "MessageTemplate", new MessageTemplateColumnWriter() },
            { "Level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
            { "Timestamp", new TimestampColumnWriter() },
            { "Exception", new ExceptionColumnWriter() },
            { "Properties", new PropertiesColumnWriter() },
        };

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Error)
            .WriteTo.PostgreSQL(
                connectionString: connectionString,
                tableName: "logs",
                columnOptions: columnWriters,
                needAutoCreateTable: false,
                restrictedToMinimumLevel: LogEventLevel.Error,
                period: TimeSpan.FromMilliseconds(50),
                batchSizeLimit: 1,
                respectCase: true)
            .CreateLogger();

        Exception captured;
        try
        {
            try
            {
                throw new InvalidOperationException($"Boom from Serilog test {marker}");
            }
            catch (Exception ex)
            {
                captured = ex;
                logger.Error(ex, "Caught test failure with marker {Marker}", marker);
            }
        }
        finally
        {
            // Disposing the logger flushes the periodic-batching sink synchronously.
            logger.Dispose();
        }

        Assert.IsType<InvalidOperationException>(captured);

        // Verify the row is present. The flush is synchronous on dispose, but
        // give the connection a single quick retry in case the cluster is slow.
        Log? row = null;
        for (var attempt = 0; attempt < 10 && row is null; attempt++)
        {
            row = await TryReadLogAsync(connectionString, marker);
            if (row is null)
                await Task.Delay(100);
        }

        Assert.NotNull(row);
        Assert.Equal("Error", row!.Level);
        Assert.Contains(marker, row.Message);
        Assert.NotNull(row.Exception);
        Assert.Contains(nameof(InvalidOperationException), row.Exception);
        Assert.Contains(marker, row.Exception);

        // Clean up so the test does not pollute the logs table on repeated runs.
        await DeleteLogAsync(connectionString, row.Id);
    }

    private sealed record Log(int Id, string Level, string Message, string? Exception);

    private static async Task<Log?> TryReadLogAsync(string connectionString, string marker)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT "Id", "Level", "Message", "Exception"
            FROM public.logs
            WHERE "Message" LIKE @marker OR "Exception" LIKE @marker
            ORDER BY "Id" DESC
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("@marker", "%" + marker + "%");

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new Log(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            await reader.IsDBNullAsync(3) ? null : reader.GetString(3));
    }

    private static async Task DeleteLogAsync(string connectionString, int id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "DELETE FROM public.logs WHERE \"Id\" = @id;",
            connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static string ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly)
            .Build();

        return config.GetConnectionString("DefaultConnection") ?? string.Empty;
    }
}

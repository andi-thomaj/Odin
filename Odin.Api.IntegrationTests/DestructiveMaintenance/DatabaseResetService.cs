using Microsoft.EntityFrameworkCore;
using Npgsql;
using Odin.Api.Data;

namespace Odin.Api.IntegrationTests.DestructiveMaintenance;

/// <summary>
/// Shared destructive helper used by the manual dev / prod reset tests.
/// Connects to the cluster's <c>postgres</c> maintenance database, terminates other
/// sessions on the target DB, drops it, recreates it, then applies EF migrations.
/// </summary>
internal static class DatabaseResetService
{
    public static async Task<DropRecreateResult> DropRecreateAndMigrateAsync(
        string targetConnectionString,
        CancellationToken ct = default)
    {
        var targetCsb = new NpgsqlConnectionStringBuilder(targetConnectionString);
        var dbName = targetCsb.Database;
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException("Target connection string must include a Database.");

        var masterCsb = new NpgsqlConnectionStringBuilder(targetConnectionString) { Database = "postgres" };
        var quotedDb = $"\"{dbName.Replace("\"", "\"\"")}\"";

        await using (var masterConn = new NpgsqlConnection(masterCsb.ConnectionString))
        {
            await masterConn.OpenAsync(ct);

            await using (var terminate = new NpgsqlCommand(
                """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @db AND pid <> pg_backend_pid()
                """,
                masterConn))
            {
                terminate.Parameters.AddWithValue("@db", dbName);
                await terminate.ExecuteNonQueryAsync(ct);
            }

            // CA2100: DDL identifiers cannot be parameterized. dbName comes from the caller's
            // connection-string Database, double-quoted via Postgres identifier rules with
            // embedded quotes doubled — the call sites already validate dbName against
            // dev/prod allow/deny lists before reaching here.
#pragma warning disable CA2100
            await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {quotedDb}", masterConn))
                await drop.ExecuteNonQueryAsync(ct);

            await using (var create = new NpgsqlCommand($"CREATE DATABASE {quotedDb}", masterConn))
                await create.ExecuteNonQueryAsync(ct);
#pragma warning restore CA2100
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(targetConnectionString, npgsql => npgsql.CommandTimeout(300))
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync(ct);

        var applied = (await context.Database.GetAppliedMigrationsAsync(ct)).ToList();
        if (applied.Count == 0)
            throw new InvalidOperationException(
                "Migrations did not apply: __EFMigrationsHistory is empty after MigrateAsync.");

        return new DropRecreateResult(dbName, applied);
    }
}

internal sealed record DropRecreateResult(string DatabaseName, IReadOnlyList<string> AppliedMigrations);

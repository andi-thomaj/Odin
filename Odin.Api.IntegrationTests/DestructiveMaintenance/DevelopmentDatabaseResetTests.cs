using Npgsql;
using Xunit;

namespace Odin.Api.IntegrationTests.DestructiveMaintenance;

/// <summary>
/// MANUAL DESTRUCTIVE OPERATION — drops and recreates the DEVELOPMENT database, then applies migrations.
///
/// This test will NEVER run in CI or default test runs: without the env vars below it skips cleanly.
/// To run it (intended for the project owner only):
///
///   1. Set ODIN_DEV_DB_CONNECTION_STRING to the dev DB connection string.
///   2. Set ODIN_DEV_DB_RESET_CONFIRM=YES_I_AM_SURE
///   3. Execute (PowerShell):
///        $env:ODIN_DEV_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=ancestrify_development;Username=odin;Password=odin_secret"
///        $env:ODIN_DEV_DB_RESET_CONFIRM = "YES_I_AM_SURE"
///        dotnet test Odin/Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj `
///          --filter "FullyQualifiedName~ForceResetDevelopmentDatabase"
///
/// Safety:
///   - Connection string Database name must NOT contain production-like tokens.
///   - The cluster's `postgres` maintenance DB is used to drop/recreate.
///   - All other sessions on the target DB are terminated first.
/// </summary>
public sealed class DevelopmentDatabaseResetTests
{
    private const string ConfirmEnvVar = "ODIN_DEV_DB_RESET_CONFIRM";
    private const string ConfirmSentinel = "YES_I_AM_SURE";
    private const string ConnectionEnvVar = "ODIN_DEV_DB_CONNECTION_STRING";

    private static readonly string[] ProductionLikeTokens =
        ["prod", "production", "live"];

    [SkippableFact]
    [Trait("Category", "DestructiveDatabase")]
    [Trait("Category", "ManualOnly")]
    public async Task ForceResetDevelopmentDatabase()
    {
        var confirm = Environment.GetEnvironmentVariable(ConfirmEnvVar);
        Skip.IfNot(string.Equals(confirm, ConfirmSentinel, StringComparison.Ordinal),
            $"Skipped: set {ConfirmEnvVar}={ConfirmSentinel} to enable. " +
            "See file header for full unlock instructions.");

        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        Skip.If(string.IsNullOrWhiteSpace(connectionString),
            $"Skipped: set {ConnectionEnvVar} to the development database connection string.");

        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var dbName = csb.Database;
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException($"{ConnectionEnvVar} has no Database.");

        var lower = dbName.ToLowerInvariant();
        foreach (var prodToken in ProductionLikeTokens)
        {
            if (lower.Contains(prodToken))
                throw new InvalidOperationException(
                    $"Refusing to reset: dev DB name '{dbName}' contains production-like token '{prodToken}'. " +
                    "Use the production-specific reset test for production databases.");
        }

        var result = await DatabaseResetService.DropRecreateAndMigrateAsync(connectionString!);

        Assert.Equal(dbName, result.DatabaseName);
        Assert.NotEmpty(result.AppliedMigrations);
    }
}

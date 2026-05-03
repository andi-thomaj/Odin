using Npgsql;
using Xunit;

namespace Odin.Api.IntegrationTests.DestructiveMaintenance;

/// <summary>
/// MANUAL CATASTROPHIC OPERATION — drops and recreates the PRODUCTION database, then applies migrations.
/// EVERY ROW IN PRODUCTION WILL BE PERMANENTLY DELETED. THIS TEST DOES NOT BACK ANYTHING UP.
///
/// This test will NEVER run in CI or default test runs: without the env vars below it skips cleanly.
/// Four independent gates must be opened to make it run; this is intentional. To run it
/// (intended for the project owner only):
///
///   1. TAKE A FULL BACKUP OF PRODUCTION FIRST. NOTHING HERE WILL DO THAT FOR YOU.
///   2. Set ODIN_PROD_DB_CONNECTION_STRING to the prod DB connection string.
///   3. Set ODIN_PROD_DB_RESET_CONFIRM=I_AM_DELETING_PRODUCTION_DATA
///   4. Set ODIN_PROD_DB_RESET_DBNAME to the EXACT Database name in the connection string above.
///      (Double-entry confirmation — this exists so you must type the prod DB name explicitly.)
///   5. Execute (PowerShell):
///        $env:ODIN_PROD_DB_CONNECTION_STRING = "Host=...;Database=ancestrify_production;Username=...;Password=..."
///        $env:ODIN_PROD_DB_RESET_CONFIRM = "I_AM_DELETING_PRODUCTION_DATA"
///        $env:ODIN_PROD_DB_RESET_DBNAME = "ancestrify_production"
///        dotnet test Odin/Odin.Api.IntegrationTests/Odin.Api.IntegrationTests.csproj `
///          --filter "FullyQualifiedName~ForceResetProductionDatabase"
///
/// Safety gates (all must pass):
///   - Skip sentinel env var must equal the literal "I_AM_DELETING_PRODUCTION_DATA".
///   - Connection-string env var must be set.
///   - DB-name confirmation env var must equal the Database in the connection string.
///   - DB name must NOT contain dev/test/staging tokens.
///   - The cluster's `postgres` maintenance DB is used to drop/recreate.
///   - All other sessions on the target DB are terminated first.
/// </summary>
public sealed class ProductionDatabaseResetTests
{
    private const string ConfirmEnvVar = "ODIN_PROD_DB_RESET_CONFIRM";
    private const string ConfirmSentinel = "I_AM_DELETING_PRODUCTION_DATA";
    private const string ConnectionEnvVar = "ODIN_PROD_DB_CONNECTION_STRING";
    private const string DbNameConfirmEnvVar = "ODIN_PROD_DB_RESET_DBNAME";

    private static readonly string[] NonProductionTokens =
        ["dev", "development", "test", "testing", "integration", "local", "staging", "qa"];

    [SkippableFact]
    [Trait("Category", "DestructiveDatabase")]
    [Trait("Category", "ManualOnly")]
    [Trait("Category", "ProductionDestructive")]
    public async Task ForceResetProductionDatabase()
    {
        var confirm = Environment.GetEnvironmentVariable(ConfirmEnvVar);
        Skip.IfNot(string.Equals(confirm, ConfirmSentinel, StringComparison.Ordinal),
            $"Skipped: set {ConfirmEnvVar}={ConfirmSentinel} to enable. " +
            "READ THE FILE HEADER BEFORE RUNNING.");

        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        Skip.If(string.IsNullOrWhiteSpace(connectionString),
            $"Skipped: set {ConnectionEnvVar} to the production database connection string.");

        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var dbName = csb.Database;
        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException($"{ConnectionEnvVar} has no Database.");

        var dbNameConfirm = Environment.GetEnvironmentVariable(DbNameConfirmEnvVar);
        if (!string.Equals(dbNameConfirm, dbName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{DbNameConfirmEnvVar} ('{dbNameConfirm ?? "<unset>"}') must equal the connection string Database " +
                $"('{dbName}'). This double-entry confirmation is required to reset production.");

        var lower = dbName.ToLowerInvariant();
        foreach (var nonProdToken in NonProductionTokens)
        {
            if (lower.Contains(nonProdToken))
                throw new InvalidOperationException(
                    $"Refusing to reset: prod DB name '{dbName}' contains non-production token '{nonProdToken}'. " +
                    "Use the development reset test for non-production databases.");
        }

        var result = await DatabaseResetService.DropRecreateAndMigrateAsync(connectionString!);

        Assert.Equal(dbName, result.DatabaseName);
        Assert.NotEmpty(result.AppliedMigrations);
    }
}

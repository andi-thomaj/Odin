using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;

namespace Odin.Api.Data
{
    public static class InitializerExtensions
    {
        public static async Task InitializeDatabaseAsync(this WebApplication application)
        {
            await using var scope = application.Services.CreateAsyncScope();

            var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();

            await initializer.InitialiseAsync();
        }
    }

    public class ApplicationDbContextInitializer(
        ApplicationDbContext context,
        DatabaseSeeder seeder,
        ILogger<ApplicationDbContextInitializer> logger)
    {
        public async Task InitialiseAsync()
        {
            try
            {
                logger.LogInformation("Initializing database...");
                await HandleMigrationConsolidationAsync();
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migration completed successfully");

                await seeder.SeedAsync();
                logger.LogInformation("Database seeding completed successfully");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "28P01")
            {
                logger.LogError(ex,
                    "Authentication failed for PostgreSQL. Please verify your connection string credentials.");
                throw new InvalidOperationException(
                    "Database authentication failed. Ensure PostgreSQL is running with correct credentials. " +
                    "Run 'docker start odin_postgres' or execute 'scripts/setup-postgres.sh' to set up the database.",
                    ex);
            }
            catch (Npgsql.NpgsqlException ex)
            {
                logger.LogError(ex, "Failed to connect to PostgreSQL database.");
                throw new InvalidOperationException(
                    "Cannot connect to PostgreSQL. Ensure the database is running. " +
                    "Run 'docker start odin_postgres' or execute 'scripts/setup-postgres.sh' to set up the database.",
                    ex);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while initializing the database");
                throw;
            }
        }

        /// <summary>
        /// Handles migration consolidation: when all previous migrations were squashed into
        /// InitialCreate but the database already has some/all tables from prior migrations.
        /// Applies the migration idempotently (CREATE TABLE IF NOT EXISTS) so existing tables
        /// are kept and missing tables are created.
        /// </summary>
        private async Task HandleMigrationConsolidationAsync()
        {
            var allMigrations = context.Database.GetMigrations().ToList();
            var initialCreate = allMigrations.FirstOrDefault(m => m.EndsWith("_InitialCreate"));
            if (initialCreate is null)
                return;

            var conn = context.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                // Check if at least one table exists (not a fresh database)
                await using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT COUNT(*) FROM information_schema.tables " +
                    "WHERE table_schema = 'public' AND table_name = 'application_users'";
                if (Convert.ToInt64(await cmd.ExecuteScalarAsync()) == 0)
                    return;

                // Collect expected tables from the EF model
                var modelTables = context.Model.GetEntityTypes()
                    .Select(e => e.GetTableName())
                    .Where(t => t is not null)
                    .ToHashSet();

                cmd.CommandText =
                    "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";
                var existingTables = new HashSet<string>();
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        existingTables.Add(reader.GetString(0));
                }

                var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

                if (modelTables.IsSubsetOf(existingTables) && pending.Contains(initialCreate))
                {
                    // All tables exist — just record the migration
                    logger.LogInformation(
                        "All tables exist — marking '{Migration}' as applied", initialCreate);
                    await context.Database.ExecuteSqlRawAsync(
                        "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                        "VALUES ({0}, {1}) ON CONFLICT DO NOTHING",
                        initialCreate, ProductInfo.GetVersion());
                    return;
                }

                var missing = modelTables.Except(existingTables).ToList();
                if (missing.Count == 0)
                    return;

                // Some tables are missing — apply the migration idempotently
                logger.LogWarning(
                    "Missing {Count} table(s): {Tables} — applying idempotent migration",
                    missing.Count, string.Join(", ", missing));

                // Remove any incorrect migration record from a prior run
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = {0}",
                    initialCreate);

                var migrator = context.GetService<IMigrator>();
                var sql = migrator.GenerateScript(
                    fromMigration: Migration.InitialDatabase,
                    toMigration: initialCreate);

                // Make CREATE statements idempotent for PostgreSQL
                // Use negative lookahead to avoid doubling "IF NOT EXISTS" on statements that already have it
                sql = Regex.Replace(sql, @"CREATE TABLE (?!IF NOT EXISTS)", "CREATE TABLE IF NOT EXISTS ");
                sql = Regex.Replace(sql, @"CREATE UNIQUE INDEX (?!IF NOT EXISTS)", "CREATE UNIQUE INDEX IF NOT EXISTS ");
                sql = Regex.Replace(sql, @"CREATE INDEX (?!IF NOT EXISTS)", "CREATE INDEX IF NOT EXISTS ");

                await context.Database.ExecuteSqlRawAsync(sql);
                logger.LogInformation("Applied idempotent InitialCreate migration successfully");
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}

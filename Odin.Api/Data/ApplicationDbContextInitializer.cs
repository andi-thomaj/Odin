using Microsoft.EntityFrameworkCore;
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
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migration completed successfully");

                await seeder.SeedAsync();
                logger.LogInformation("Database seeding completed successfully");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "28P01")
            {
                logger.LogError(ex, "Authentication failed for PostgreSQL. Please verify your connection string credentials.");
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
    }
}

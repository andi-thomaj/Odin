using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;

namespace Odin.Api.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture = new();

    public string ConnectionString => _dbFixture.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor =
                services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using the test container connection string
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_dbFixture.ConnectionString));

            // Register ApplicationDbContextInitializer for tests
            services.AddScoped<ApplicationDbContextInitializer>();
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _dbFixture.InitializeAsync();

        // Create database schema before the host starts, so the seeder
        // can query tables during app startup (MigrateAsync is a no-op
        // without real migration files).
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_dbFixture.ConnectionString)
            .Options;
        await using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        // Accessing Services triggers the host to start (Program.Main),
        // which runs MigrateAsync + SeedAsync — tables now exist.
        _ = Services;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbFixture.DisposeAsync();
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Odin.Api.Data;
using Odin.Api.Endpoints.AuthRegistration;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.Services.Email;
using Respawn;
using Respawn.Graph;

namespace Odin.Api.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Resolved before the host is built (see <see cref="ResolveConnectionString"/>).
    /// </summary>
    private readonly string _connectionString = ResolveConnectionString();

    public string ConnectionString => _connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = _connectionString });
        });

        builder.ConfigureTestServices(services =>
        {
            var descriptor =
                services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_connectionString));

            services.AddScoped<ApplicationDbContextInitializer>();

            var auth0Signup = services.SingleOrDefault(d => d.ServiceType == typeof(IAuth0DatabaseSignupClient));
            if (auth0Signup is not null)
                services.Remove(auth0Signup);
            services.AddSingleton<IAuth0DatabaseSignupClient, FakeAuth0DatabaseSignupClient>();

            foreach (var d in services.Where(x => x.ServiceType == typeof(IResendAudienceService)).ToList())
                services.Remove(d);
            services.AddSingleton<IResendAudienceService, NoOpResendAudienceService>();
        });
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync();
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException(
                "Integration tests require PostgreSQL. Set ConnectionStrings__DefaultConnection or align " +
                "Odin.Api/appsettings.Testing.json with your server. Create the database if needed: " +
                "CREATE DATABASE odin_integration_test;",
                ex);
        }

        // Wipe all data left over from a previous test run so the startup
        // seeder's AnyAsync() guards don't skip on stale rows.
        var respawner = await Respawner.CreateAsync(connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")]
            });
        await respawner.ResetAsync(connection);

        // Host startup runs MigrateAsync + SeedAsync on the now-clean database.
        _ = Services;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    /// <summary>
    /// 1) Environment variable <c>ConnectionStrings__DefaultConnection</c> (full override).<br/>
    /// 2) <see cref="ApplicationDbContext"/> assembly user secrets (<c>dotnet user secrets</c> on Odin.Api), with
    /// <c>ancestrify_development</c> / <c>odin_db</c> rewritten to <c>odin_integration_test</c>.<br/>
    /// 3) Literal fallback aligned with <c>Odin.Api/appsettings.Testing.json</c>.
    /// </summary>
    private static string ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly)
            .AddEnvironmentVariables()
            .Build();

        var fromSecrets = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromSecrets))
        {
            var csb = new NpgsqlConnectionStringBuilder(fromSecrets);
            if (csb.Database is "ancestrify_development" or "odin_db")
                csb.Database = "odin_integration_test";
            return csb.ConnectionString;
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Database = "odin_integration_test",
            Username = "odin",
            Password = "odin_secret"
        }.ConnectionString;
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Odin.Api.Data;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.Services.Email;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Odin.Api.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Container instance — null when an external database is supplied via env var.</summary>
    private readonly PostgreSqlContainer? _container;

    /// <summary>External connection string supplied via <c>ConnectionStrings__DefaultConnection</c>; null otherwise.</summary>
    private readonly string? _externalConnectionString;

    /// <summary>Resolved at <see cref="InitializeAsync"/>; throws if accessed before initialization.</summary>
    private string? _connectionString;

    public string ConnectionString => _connectionString
        ?? throw new InvalidOperationException(
            $"{nameof(CustomWebApplicationFactory)}.{nameof(InitializeAsync)} must run before {nameof(ConnectionString)} is read.");

    public CustomWebApplicationFactory()
    {
        _externalConnectionString = ResolveExternalConnectionString();

        if (_externalConnectionString is not null)
        {
            // External Postgres — skip the container; tests will hit whatever the env var points at.
            return;
        }

        // Default path: a disposable Postgres container per factory lifetime.
        // The factory is a collection fixture (see IntegrationTestCollection), so the
        // container is shared across every test in the suite and torn down at the end.
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("odin_integration_test")
            .WithUsername("odin")
            .WithPassword("odin_secret")
            .WithCleanUp(true)
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = ConnectionString });
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
                options.UseNpgsql(ConnectionString));

            services.AddScoped<ApplicationDbContextInitializer>();

            foreach (var d in services.Where(x => x.ServiceType == typeof(IResendAudienceService)).ToList())
                services.Remove(d);
            services.AddSingleton<IResendAudienceService, NoOpResendAudienceService>();
        });
    }

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            // Fresh container — no stale data and no tables yet. Host startup will run
            // MigrateAsync + SeedAsync once Services is realised below; per-test Respawn
            // (in IntegrationTestBase) takes over from there.
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
            _ = Services;
            return;
        }

        // External database path: validate connectivity, then wipe any leftover data so the
        // startup seeder's AnyAsync() guards don't skip on stale rows.
        _connectionString = _externalConnectionString!;
        await using var connection = new NpgsqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync();
        }
        catch (NpgsqlException ex)
        {
            throw new InvalidOperationException(
                $"Integration tests could not reach the external database supplied via " +
                "ConnectionStrings__DefaultConnection. Unset the variable to fall back to the " +
                "Testcontainers Postgres instance, or align the database with " +
                "Odin.Api/appsettings.Testing.json. Create the database if needed: " +
                "CREATE DATABASE odin_integration_test;",
                ex);
        }

        var respawner = await Respawner.CreateAsync(connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")]
            });
        await respawner.ResetAsync(connection);

        _ = Services;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Returns the explicitly supplied connection string (env var
    /// <c>ConnectionStrings__DefaultConnection</c>) when present, or null to fall back to the
    /// managed Postgres container.
    /// </summary>
    private static string? ResolveExternalConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv.Trim();
    }
}

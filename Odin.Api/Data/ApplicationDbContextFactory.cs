using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Odin.Api.Authentication;

namespace Odin.Api.Data
{
    /// <summary>
    /// Design-time factory so <c>dotnet ef migrations</c> can construct <see cref="ApplicationDbContext"/>,
    /// which now requires an <see cref="IAppContext"/>. The app value is irrelevant at design time — query
    /// filters are evaluated only at query execution, never during model build — so a default
    /// <see cref="RequestAppContext"/> (app = <see cref="AppKeys.Ancestrify"/>) is sufficient. Connection
    /// string comes from <c>ConnectionStrings:DefaultConnection</c> (appsettings or the
    /// <c>ConnectionStrings__DefaultConnection</c> env var used by the throwaway-DB gen flow).
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Database=odin;Username=postgres;Password=postgres";

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new ApplicationDbContext(options, new RequestAppContext());
        }
    }
}

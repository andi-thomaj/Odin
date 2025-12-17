using Microsoft.EntityFrameworkCore;

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

    public class ApplicationDbContextInitializer(ApplicationDbContext context)
    {
        public async Task InitialiseAsync()
        {
            try
            {
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.UserManagement;

namespace Odin.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;
            var services = builder.Services;

            services.AddAuthorization();
            services.AddOpenApi();
            services.AddSwaggerGen();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            services.AddValidation();
            services.AddScoped<IUserService, UserService>();

            var app = builder.Build();

            await app.InitializeDatabaseAsync();
            
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // app.UseAuthentication();
            app.UseAuthorization();

            app.MapUserEndpoints();
            await app.RunAsync();
        }
    }
}

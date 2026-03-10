using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.GeneticInspectionManagement;
using Odin.Api.Endpoints.NotificationManagement;
using Odin.Api.Endpoints.OrderManagement;
using Odin.Api.Endpoints.RawGeneticFileManagement;
using Odin.Api.Endpoints.UserManagement;
using Odin.Api.Hubs;
using Odin.Api.Middleware;

namespace Odin.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;
            var services = builder.Services;

            // ── Authentication (Auth0 JWT) ──────────────────────────────
            var auth0Domain = configuration["Jwt:Authority"]!;
            var auth0Audience = configuration["Jwt:Audience"]!;

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = auth0Domain;
                    options.Audience = auth0Audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken)
                                && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            // ── Authorization policies ──────────────────────────────────
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireAuthenticatedUser()
                        .RequireClaim("app_role", AppRole.Admin.ToString()));

                options.AddPolicy("ScientistOrAdmin", policy =>
                    policy.RequireAuthenticatedUser()
                        .RequireClaim("app_role",
                            AppRole.Scientist.ToString(),
                            AppRole.Admin.ToString()));

                options.AddPolicy("Authenticated", policy =>
                    policy.RequireAuthenticatedUser());
            });

            services.AddOpenApi();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
            services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
            });
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            services.AddScoped<ApplicationDbContextInitializer>();
            services.AddScoped<DatabaseSeeder>();
            services.AddValidation();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IEthnicityService, EthnicityService>();
            services.AddScoped<IEraService, EraService>();
            services.AddScoped<IRawGeneticFileService, RawGeneticFileService>();
            services.AddScoped<IGeneticInspectionService, GeneticInspectionService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<INotificationService, NotificationService>();

            services.AddSignalR();
            services.AddSingleton<IUserIdProvider, UserIdProvider>();

            var app = builder.Build();

            await app.InitializeDatabaseAsync();

            app.UseStaticFiles();
            app.UseCors("AllowFrontend");

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.InjectStylesheet("/swagger-ui/dark-mode.css");
                    options.InjectJavascript("/swagger-ui/dark-mode-toggle.js");
                });
            }

            app.UseAuthentication();
            app.UseRoleEnrichment();
            app.UseAuthorization();

            app.MapHub<NotificationHub>("/hubs/notifications");

            app.MapUserEndpoints();
            app.MapRawGeneticFileEndpoints();
            app.MapGeneticInspectionEndpoints();
            app.MapOrderEndpoints();
            app.MapNotificationEndpoints();
            await app.RunAsync();
        }
    }
}

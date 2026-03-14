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
using Odin.Api.Endpoints.ReferenceDataManagement;
using Odin.Api.Endpoints.ReportManagement;
using Odin.Api.Endpoints.UserManagement;
using Odin.Api.Hubs;
using Odin.Api.Middleware;
using Odin.Api.Services;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using NpgsqlTypes;

namespace Odin.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;
            var services = builder.Services;

            // ── Serilog (Error-only → PostgreSQL) ───────────────────────
            var connectionString = configuration.GetConnectionString("DefaultConnection")!;

            var columnWriters = new Dictionary<string, ColumnWriterBase>
            {
                { "message", new RenderedMessageColumnWriter() },
                { "message_template", new MessageTemplateColumnWriter() },
                { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
                { "timestamp", new TimestampColumnWriter() },
                { "exception", new ExceptionColumnWriter() },
                { "properties", new PropertiesColumnWriter() },
            };

            builder.Host.UseSerilog((context, loggerConfig) =>
            {
                loggerConfig
                    .MinimumLevel.Error()
                    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Error)
                    .WriteTo.PostgreSQL(
                        connectionString: connectionString,
                        tableName: "logs",
                        columnOptions: columnWriters,
                        needAutoCreateTable: false,
                        restrictedToMinimumLevel: LogEventLevel.Error
                    );
            });

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

            services.AddMemoryCache();

            // ── Response Compression ────────────────────────────────────
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });
            services.Configure<BrotliCompressionProviderOptions>(options =>
                options.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(options =>
                options.Level = CompressionLevel.Fastest);

            // ── Rate Limiting ───────────────────────────────────────────
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("authenticated", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));
                options.AddPolicy("file-upload", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));
            });

            // ── Health Checks ───────────────────────────────────────────
            services.AddHealthChecks()
                .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!);

            services.AddOpenApi();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:4200", "https://localhost:4200", "http://localhost:3000")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
            services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
            });
            services.AddDbContextPool<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions => npgsqlOptions.CommandTimeout(15)));
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
            services.AddScoped<IReportService, ReportService>();
            services.AddHttpClient<IGeoLocationService, GeoLocationService>();

            services.AddSignalR();
            services.AddSingleton<IUserIdProvider, UserIdProvider>();

            var app = builder.Build();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                                   Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
            });

            await app.InitializeDatabaseAsync();

            app.UseResponseCompression();
            app.UseStaticFiles();
            app.UseCors("AllowFrontend");
            app.UseRateLimiter();

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
            app.MapHealthChecks("/health");

            app.MapUserEndpoints();
            app.MapEthnicityEndpoints();
            app.MapEraEndpoints();
            app.MapRawGeneticFileEndpoints();
            app.MapGeneticInspectionEndpoints();
            app.MapOrderEndpoints();
            app.MapNotificationEndpoints();
            app.MapReportEndpoints();
            await app.RunAsync();
        }
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Odin.Api.Authentication;
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
using Odin.Api.Models;
using Odin.Api.Services;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using NpgsqlTypes;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Timeouts;

namespace Odin.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;
            var services = builder.Services;

            // ── Serilog (Console + PostgreSQL) ────────────────────────────
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
                // Read log level from configuration (defaults to Information)
                var logLevelString = context.Configuration["Logging:LogLevel:Default"] ?? "Information";
                var consoleLogLevel = Enum.TryParse<LogEventLevel>(logLevelString, ignoreCase: true, out var parsedLevel)
                    ? parsedLevel
                    : LogEventLevel.Information;

                // In Development, ensure at least Information level for console
                if (context.HostingEnvironment.IsDevelopment() && consoleLogLevel > LogEventLevel.Information)
                {
                    consoleLogLevel = LogEventLevel.Information;
                }

                loggerConfig
                    .MinimumLevel.Is(consoleLogLevel)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .WriteTo.Console(restrictedToMinimumLevel: consoleLogLevel)
                    .WriteTo.PostgreSQL(
                        connectionString: connectionString,
                        tableName: "logs",
                        columnOptions: columnWriters,
                        needAutoCreateTable: false,
                        restrictedToMinimumLevel: LogEventLevel.Error
                    );
            });

            // ── Authentication (JWT in app; test scheme for integration tests) ──
            if (builder.Environment.IsEnvironment("Testing"))
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            }
            else
            {
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
            }

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
                options.OnRejected = async (context, cancellationToken) =>
                {
                    // Extract RetryAfter from lease metadata if available
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter.Name, out var retryAfterObj) 
                        && retryAfterObj is TimeSpan retryAfter)
                    {
                        context.HttpContext.Response.Headers["Retry-After"] = 
                            ((int)retryAfter.TotalSeconds).ToString();
                    }
                    
                    // Extract policy name from endpoint metadata
                    var policyName = context.HttpContext.GetEndpoint()
                        ?.Metadata
                        .GetMetadata<EnableRateLimitingAttribute>()
                        ?.PolicyName ?? "unknown";
                    
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    var requestId = context.HttpContext.Items.TryGetValue("RequestId", out var id) 
                        ? id?.ToString() 
                        : "unknown";
                    
                    logger.LogWarning(
                        "Rate limit exceeded. RequestId: {RequestId}, Policy: {Policy}, Path: {Path}",
                        requestId,
                        policyName,
                        context.HttpContext.Request.Path);

                    context.HttpContext.Response.ContentType = "application/json";
                    var errorBody = new ErrorResponse
                    {
                        RequestId = requestId ?? string.Empty,
                        StatusCode = StatusCodes.Status429TooManyRequests,
                        Message = "Rate limit exceeded. Please try again later.",
                        ErrorCode = "RATE_LIMIT_EXCEEDED"
                    };
                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(errorBody, jsonOptions), cancellationToken);
                };

                // Helper to get client IP (handles X-Forwarded-For)
                static string GetClientIp(HttpContext context)
                {
                    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(forwardedFor))
                    {
                        // Take first IP from comma-separated list
                        var ip = forwardedFor.Split(',')[0].Trim();
                        if (IPAddress.TryParse(ip, out _))
                            return ip;
                    }
                    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                }

                // Helper to get partition key (user ID or IP)
                static string GetPartitionKey(HttpContext context)
                {
                    return context.User.Identity?.IsAuthenticated == true
                        ? context.User.Identity.Name ?? GetClientIp(context)
                        : GetClientIp(context);
                }

                // Global default policy - 30 req/min for unauthenticated, 100 req/min for authenticated
                // Exclude SignalR endpoints from global rate limiting (they have their own connection management)
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    // Skip rate limiting for SignalR hubs (negotiation and connection endpoints)
                    var path = context.Request.Path.Value ?? "";
                    if (path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
                    {
                        // Return a no-op limiter for SignalR endpoints
                        return RateLimitPartition.GetNoLimiter(partitionKey: "");
                    }
                    
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(context),
                        factory: _ =>
                        {
                            var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
                            return new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = isAuthenticated ? 100 : 30,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0
                            };
                        });
                });

                // Authenticated policy - sliding window for better distribution
                options.AddPolicy("authenticated", httpContext =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 4,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));

                // File upload policy - strict limits
                options.AddPolicy("file-upload", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));

                // Strict policy for sensitive/admin endpoints
                options.AddPolicy("strict", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));

                // Concurrency limiter for resource-intensive operations
                options.AddPolicy("concurrent", httpContext =>
                    RateLimitPartition.GetConcurrencyLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = 5,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));
            });

            // ── Health Checks ───────────────────────────────────────────
            services.AddHealthChecks()
                .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!);

            services.AddOpenApi();
            
            // ── CORS Configuration ───────────────────────────────────────
            var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:3000", "http://localhost:4200", "https://localhost:3000", "https://localhost:4200" };
            
            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins(corsOrigins)
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                        // SignalR requires specific headers for negotiation
                        // Allow common headers plus SignalR-specific ones
                        // Note: SignalR client may send various headers during negotiation
                        .WithHeaders(
                            "Content-Type", 
                            "Authorization", 
                            "X-Request-ID",
                            "X-SignalR-User-Agent",  // SignalR specific header
                            "x-requested-with",      // Common header SignalR may use
                            "Accept",                 // SignalR may send Accept header
                            "Cache-Control"          // SignalR may send Cache-Control
                        )
                        .AllowCredentials()
                        .SetPreflightMaxAge(TimeSpan.FromHours(24));
                });
            });
            services.AddSwaggerGen(options =>
            {
                options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
            });
            // ── Request Size Limits ─────────────────────────────────────
            services.Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = 50 * 1024 * 1024; // 50 MB
                options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
                options.MultipartHeadersLengthLimit = 16384; // 16 KB
            });

            // ── Kestrel Server Options ──────────────────────────────────
            services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB
                options.Limits.MaxRequestHeadersTotalSize = 32768; // 32 KB
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
            });

            // ── Request Timeout ──────────────────────────────────────────
            services.AddRequestTimeouts(options =>
            {
                options.AddPolicy("Default", TimeSpan.FromSeconds(30));
                options.AddPolicy("FileUpload", TimeSpan.FromMinutes(5));
            });

            services.AddDbContextPool<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                    npgsqlOptions => npgsqlOptions.CommandTimeout(15)));
            services.AddScoped<ApplicationDbContextInitializer>();
            services.AddScoped<DatabaseSeeder>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IEthnicityService, EthnicityService>();
            services.AddScoped<IEraService, EraService>();
            services.AddScoped<IRawGeneticFileService, RawGeneticFileService>();
            services.AddScoped<IGeneticInspectionService, GeneticInspectionService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddHttpClient<IGeoLocationService, GeoLocationService>();

            services.AddSignalR(options =>
            {
                // Enable detailed error messages in development
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            });
            services.AddSingleton<IUserIdProvider, UserIdProvider>();

            var app = builder.Build();

            // ── HTTPS Redirection (Production only) ──────────────────────
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            // ── Forwarded Headers (for reverse proxies) ─────────────────
            UseOdinForwardedHeaders(app, configuration);

            await app.InitializeDatabaseAsync();

            // ── Middleware Pipeline ─────────────────────────────────────
            app.UseRequestId(); // Generate request ID first
            app.UseGlobalExceptionHandler(); // Catch all exceptions
            app.UseSecurityHeaders(); // Add security headers
            app.UseResponseCompression();
            app.UseStaticFiles();
            app.UseCors("AllowFrontend");
            if (!app.Environment.IsEnvironment("Testing"))
                app.UseRateLimiter();
            app.UseRequestTimeouts();

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

        private static void UseOdinForwardedHeaders(WebApplication app, IConfiguration configuration)
        {
            var forwardedOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                RequireHeaderSymmetry = false,
                ForwardedForHeaderName = "X-Forwarded-For"
            };

            if (app.Environment.IsDevelopment())
            {
                forwardedOptions.KnownIPNetworks.Clear();
                forwardedOptions.KnownProxies.Clear();
                forwardedOptions.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("127.0.0.1/32"));
                forwardedOptions.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("::1/128"));
            }
            else if (!app.Environment.IsEnvironment("Testing"))
            {
                forwardedOptions.KnownIPNetworks.Clear();
                forwardedOptions.KnownProxies.Clear();
                foreach (var ipText in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ??
                                       [])
                {
                    if (IPAddress.TryParse(ipText, out var ip))
                        forwardedOptions.KnownProxies.Add(ip);
                }
            }

            app.UseForwardedHeaders(forwardedOptions);
        }
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Odin.Api.Authentication;
using Odin.Api.Services.Email;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.Admin;
using Odin.Api.Endpoints.CladeFinderManagement;
using Odin.Api.Endpoints.AppSettingsManagement;
using Odin.Api.Endpoints.G25PopulationSampleManagement;
using Odin.Api.Endpoints.G25DistancePopulationSampleManagement;
using Odin.Api.Endpoints.G25PcaPopulationsSampleManagement;
using Odin.Api.Endpoints.QpadmPopulationSampleManagement;
using Odin.Api.Endpoints.G25SavedCoordinateManagement;
using Odin.Api.Endpoints.G25TargetCoordinateManagement;
using Odin.Api.Endpoints.GeneticInspectionManagement;
using Odin.Api.Endpoints.NotificationManagement;
using Odin.Api.Endpoints.OrderManagement;
using Odin.Api.Endpoints.PopulationManagement;
using Odin.Api.Endpoints.RawGeneticFileManagement;
using Odin.Api.Endpoints.ReferenceDataManagement;
using Odin.Api.Endpoints.MediaManagement;
using Odin.Api.Endpoints.ReportManagement;
using Odin.Api.Endpoints.UserManagement;
using Odin.Api.Endpoints.G25Calculations;
using Odin.Api.Endpoints.G25ContinentManagement;
using Odin.Api.Endpoints.CalculatorManagement;
using Odin.Api.Endpoints.G25AdmixtureEraManagement;
using Odin.Api.Endpoints.G25DistanceEraManagement;
using Odin.Api.Endpoints.G25EthnicityManagement;
using Odin.Api.Endpoints.G25RegionManagement;
using Odin.Api.Hubs;
using Odin.Api.Middleware;
using Odin.Api.Models;
using Odin.Api.Services;
using Odin.Api.Services.AppSettings;
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
using Hangfire;
using Hangfire.PostgreSql;
using Odin.Api.Hangfire;

namespace Odin.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;
            var services = builder.Services;

            ValidateProductionDatabaseHost(configuration, builder.Environment);

            // ── Serilog (Console + PostgreSQL) ────────────────────────────
            var connectionString = configuration.GetConnectionString("DefaultConnection")!;

            // Column names match the actual Postgres `logs` table schema (PascalCase, quoted).
            // Lowercase keys (the old config) silently failed every insert because Postgres
            // treats quoted PascalCase identifiers as case-sensitive — `Message` != `message`.
            var columnWriters = new Dictionary<string, ColumnWriterBase>
            {
                { "Message", new RenderedMessageColumnWriter() },
                { "MessageTemplate", new MessageTemplateColumnWriter() },
                { "Level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
                { "Timestamp", new TimestampColumnWriter() },
                { "Exception", new ExceptionColumnWriter() },
                { "Properties", new PropertiesColumnWriter() },
            };

            builder.Host.UseSerilog((context, loggerConfig) =>
            {
                // Read the connection string from context.Configuration (not the outer captured
                // variable) so WebApplicationFactory test overrides via ConfigureAppConfiguration
                // are visible — those run AFTER the outer `connectionString` was captured.
                // Falls back to the outer value for prod/dev where the test factory isn't in play.
                var sinkConnectionString = context.Configuration.GetConnectionString("DefaultConnection")
                                           ?? connectionString;

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

                // Under integration tests, flush each log event almost immediately so assertions
                // don't have to wait out the default 5s batching window. Production keeps the
                // library defaults (5s period, batch size 50) for throughput.
                var isTesting = context.HostingEnvironment.IsEnvironment("Testing");
                var batchPeriod = isTesting ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(5);
                var batchSizeLimit = isTesting ? 1 : 50;

                loggerConfig
                    .MinimumLevel.Is(consoleLogLevel)
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .WriteTo.Console(restrictedToMinimumLevel: consoleLogLevel)
                    .WriteTo.PostgreSQL(
                        connectionString: sinkConnectionString,
                        tableName: "logs",
                        columnOptions: columnWriters,
                        needAutoCreateTable: false,
                        restrictedToMinimumLevel: LogEventLevel.Error,
                        period: batchPeriod,
                        batchSizeLimit: batchSizeLimit,
                        // `respectCase: true` is REQUIRED: the EF migration created the table with
                        // quoted PascalCase columns ("Message", "Level", ...). Without it the sink
                        // emits unquoted identifiers that Postgres folds to lowercase, every COPY
                        // fails, and the logs table stays empty.
                        respectCase: true
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
                        // Keep Auth0 short claim names (e.g. email_verified) for RoleEnrichmentMiddleware.
                        options.MapInboundClaims = false;
                        options.SaveToken = true;
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
                        // Hangfire dashboard is loaded by the browser via top-level navigation
                        // (no Authorization header). Forward those requests to the cookie scheme so
                        // HangfireDashboardAuthFilter sees a populated User. API requests keep the
                        // JWT default — they always carry a Bearer header.
                        options.ForwardDefaultSelector = context =>
                            context.Request.Path.StartsWithSegments("/jobs")
                                ? HangfireAuthScheme.Name
                                : null;
                    })
                    .AddCookie(HangfireAuthScheme.Name, options =>
                    {
                        options.Cookie.Name = "odin_hangfire_session";
                        options.Cookie.HttpOnly = true;
                        // Lax lets the cookie ride along on the top-level GET when the admin opens
                        // /jobs in a new tab from the SPA. Strict would block it. None would require
                        // Secure on http://localhost during development and break dev entirely.
                        options.Cookie.SameSite = SameSiteMode.Lax;
                        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                            ? CookieSecurePolicy.SameAsRequest
                            : CookieSecurePolicy.Always;
                        // Path-scope the cookie so it isn't sent on every API request — only on
                        // Hangfire dashboard paths under /jobs.
                        options.Cookie.Path = "/jobs";
                        options.ExpireTimeSpan = TimeSpan.FromHours(1);
                        options.SlidingExpiration = true;
                        // Dashboard is a browser-rendered admin tool, not a login UI — return raw
                        // 401/403 instead of trying to redirect to a /Account/Login that doesn't
                        // exist on this app.
                        options.Events.OnRedirectToLogin = ctx =>
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        };
                        options.Events.OnRedirectToAccessDenied = ctx =>
                        {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        };
                    });
            }

            // ── Authorization policies ──────────────────────────────────
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy =>
                    policy.RequireAuthenticatedUser()
                        .RequireClaim("app_role", AppRole.Admin.ToString())
                        .RequireClaim(AppClaimTypes.EmailVerified, "true"));

                options.AddPolicy("ScientistOrAdmin", policy =>
                    policy.RequireAuthenticatedUser()
                        .RequireClaim("app_role",
                            AppRole.Scientist.ToString(),
                            AppRole.Admin.ToString())
                        .RequireClaim(AppClaimTypes.EmailVerified, "true"));

                options.AddPolicy("Authenticated", policy =>
                    policy.RequireAuthenticatedUser());

                options.AddPolicy("EmailVerified", policy =>
                    policy.RequireAuthenticatedUser()
                        .RequireClaim(AppClaimTypes.EmailVerified, "true"));
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

            // ── CORS Configuration ───────────────────────────────────────
            var corsOrigins = BuildCorsAllowedOrigins(configuration);

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
                            "X-SignalR-User-Agent", // SignalR specific header
                            "x-requested-with", // Common header SignalR may use
                            "Accept", // SignalR may send Accept header
                            "Cache-Control", // SignalR may send Cache-Control
                            "baggage"
                        )
                        .AllowCredentials()
                        .SetPreflightMaxAge(TimeSpan.FromHours(24));
                });
            });
            // Minimal-API endpoint metadata for Swashbuckle. Without this, ISwaggerProvider
            // cannot be constructed and the per-request resolution inside UseSwagger's
            // middleware throws InvalidOperationException on every request — surfacing as
            // 400 "The request is invalid." via GlobalExceptionHandlerMiddleware.
            services.AddEndpointsApiExplorer();
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
                    npgsqlOptions => npgsqlOptions.CommandTimeout(180)));
            services.AddScoped<ApplicationDbContextInitializer>();
            services.AddScoped<DatabaseSeeder>();

            // R2 (Cloudflare object storage) — population MP4 avatars and any future media.
            services.Configure<Odin.Api.Storage.R2Options>(
                configuration.GetSection(Odin.Api.Storage.R2Options.SectionName));
            services.AddSingleton<Odin.Api.Storage.IR2Storage, Odin.Api.Storage.R2Storage>();

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserDataExportService, UserDataExportService>();
            services.AddScoped<IUserProvisioningService, UserProvisioningService>();
            services.AddScoped<IEthnicityService, EthnicityService>();
            services.AddScoped<IEraService, EraService>();
            services.AddScoped<IPopulationService, PopulationService>();
            services.AddScoped<IRawGeneticFileService, RawGeneticFileService>();
            services.AddScoped<IGeneticInspectionService, GeneticInspectionService>();
            services.AddScoped<IAppSettingsService, AppSettingsService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IMediaService, MediaService>();
            services.AddScoped<IG25PopulationSampleService, G25PopulationSampleService>();
            services.AddScoped<IG25DistancePopulationSampleService, G25DistancePopulationSampleService>();
            services.AddScoped<IG25PcaPopulationsSampleService, G25PcaPopulationsSampleService>();
            services.AddScoped<IQpadmPopulationSampleService, QpadmPopulationSampleService>();
            services.AddScoped<IG25SavedCoordinateService, G25SavedCoordinateService>();
            services.AddScoped<IG25TargetCoordinateService, G25TargetCoordinateService>();
            services.AddScoped<IG25RegionService, G25RegionService>();
            services.AddScoped<IG25EthnicityService, G25EthnicityService>();
            services.AddScoped<IG25ContinentService, G25ContinentService>();
            services.AddScoped<IG25DistanceEraService, G25DistanceEraService>();
            services.AddScoped<IG25AdmixtureEraService, G25AdmixtureEraService>();
            services.AddScoped<IG25CalculationService, G25CalculationService>();
            services.AddScoped<ICalculatorService, CalculatorService>();
            services.AddScoped<IAdmixToolsEraService, AdmixToolsEraService>();
            services.AddScoped<ILogCleanupService, LogCleanupService>();
            services.AddScoped<Odin.Api.Endpoints.Admin.IG25SeedImportService, Odin.Api.Endpoints.Admin.G25SeedImportService>();
            services.AddHttpClient<IGeoLocationService, GeoLocationService>();

            services.Configure<ResendEmailOptions>(configuration.GetSection(ResendEmailOptions.SectionName));
            services.Configure<AppPublicOptions>(configuration.GetSection(AppPublicOptions.SectionName));
            services.Configure<Odin.Api.Configuration.OrderLimitsOptions>(
                configuration.GetSection(Odin.Api.Configuration.OrderLimitsOptions.SectionName));

            services.AddHttpClient<IResendAudienceService, ResendAudienceService>((_, client) =>
            {
                client.BaseAddress = new Uri("https://api.resend.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.Configure<Odin.Api.Configuration.ToolsApiOptions>(
                configuration.GetSection(Odin.Api.Configuration.ToolsApiOptions.SectionName));
            services.AddHttpClient<
                Odin.Api.Endpoints.CladeFinderManagement.ICladeFinderService,
                Odin.Api.Endpoints.CladeFinderManagement.CladeFinderService>((sp, client) =>
            {
                var toolsOptions = sp.GetRequiredService<IOptions<Odin.Api.Configuration.ToolsApiOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(toolsOptions.BaseUrl))
                {
                    client.BaseAddress = new Uri(toolsOptions.BaseUrl);
                }
                client.Timeout = TimeSpan.FromSeconds(toolsOptions.TimeoutSeconds);
            });
            services.AddHttpClient("Auth0UserInfo", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });
            services.AddSignalR(options =>
            {
                // Enable detailed error messages in development
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            });
            services.AddSingleton<IUserIdProvider, UserIdProvider>();

            // ── Hangfire (background jobs) ───────────────────────────────
            // DI services (IBackgroundJobClient, IRecurringJobManager, ...) are registered in
            // every environment so endpoints that inject them — e.g. RecomputeDistanceResults
            // in G25AdminEndpoints — can be bound during host startup. ASP.NET Core eagerly
            // resolves every endpoint's parameters when the pipeline builds, so a missing
            // service here fails the entire test host with "Failure to infer one or more
            // parameters". UsePostgreSqlStorage does not open a connection at registration
            // time; tests never enqueue jobs or start the server, so the storage stays cold.
            services.AddHangfire(hf => hf
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

            // Worker process — skipped in Testing so the suite doesn't poll the throwaway
            // Postgres container or compete with Respawn for table locks.
            if (!builder.Environment.IsEnvironment("Testing"))
            {
                services.AddHangfireServer(opts =>
                {
                    opts.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
                });
            }

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
            app.UseCors("AllowFrontend");
            app.UseStaticFiles();
            if (!app.Environment.IsEnvironment("Testing"))
                app.UseRateLimiter();
            app.UseRequestTimeouts();

            // Swagger only in Development. The middleware resolves ISwaggerProvider per
            // request, so leaving it on in Production turns every endpoint into a 400 if
            // anything in the OpenAPI graph fails to construct.
            if (app.Environment.IsDevelopment())
            {
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

            // Hangfire dashboard — admin-only. Not mounted in Testing.
            if (!app.Environment.IsEnvironment("Testing"))
            {
                app.UseHangfireDashboard("/jobs", new DashboardOptions
                {
                    Authorization = new[] { new HangfireDashboardAuthFilter() },
                    DashboardTitle = "Odin background jobs"
                });

                // Recurring jobs. Registered after the Hangfire storage is wired (see AddHangfire above)
                // and skipped in Testing because Hangfire itself is not registered there.
                //
                // logs-cleanup: wipe the `logs` table every 5 days at 03:00 UTC. Cron day-of-month
                // `*/5` evaluates to days 1,6,11,16,21,26,31 — i.e. roughly every 5 days with a
                // 1-day skew at month boundaries. That's acceptable for a logs-retention job.
                var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
                recurringJobManager.AddOrUpdate<ILogCleanupService>(
                    recurringJobId: "logs-cleanup",
                    methodCall: svc => svc.DeleteAllLogsAsync(CancellationToken.None),
                    cronExpression: "0 3 */5 * *",
                    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
            }

            // Version prefix — all business endpoints live under /v1 so breaking changes
            // can ship a v2 alongside without breaking existing clients. SignalR hubs and
            // health checks stay at the root by convention (not part of the API surface).
            var v1 = app.MapGroup("/v1");
            v1.MapUserEndpoints();
            v1.MapEthnicityEndpoints();
            v1.MapEraEndpoints();
            v1.MapPopulationEndpoints();
            v1.MapRawGeneticFileEndpoints();
            v1.MapGeneticInspectionEndpoints();
            v1.MapOrderEndpoints();
            v1.MapAppSettingsEndpoints();
            v1.MapNotificationEndpoints();
            v1.MapReportEndpoints();
            v1.MapMediaEndpoints();
            v1.MapG25PopulationSampleEndpoints();
            v1.MapG25DistancePopulationSampleEndpoints();
            v1.MapG25PcaPopulationsSampleEndpoints();
            v1.MapQpadmPopulationSampleEndpoints();
            v1.MapG25SavedCoordinateEndpoints();
            v1.MapG25TargetCoordinateEndpoints();
            v1.MapG25RegionEndpoints();
            v1.MapG25EthnicityEndpoints();
            v1.MapG25ContinentEndpoints();
            v1.MapG25DistanceEraEndpoints();
            v1.MapG25AdmixtureEraEndpoints();
            v1.MapG25CalculationEndpoints();
            v1.MapG25AdminEndpoints();
            v1.MapHangfireSessionEndpoints();
            v1.MapCalculatorEndpoints();
            v1.MapAdmixToolsEraEndpoints();
            v1.MapCladeFinderEndpoints();

            // Testing-only diagnostics: lets integration tests force an unhandled exception
            // through GlobalExceptionHandlerMiddleware to verify Serilog → logs table persistence.
            // Never registered outside the Testing host environment.
            if (app.Environment.IsEnvironment("Testing"))
            {
                v1.MapGet("/api/diagnostics/throw", (string? marker) =>
                {
                    throw new InvalidOperationException(
                        $"Diagnostics throw triggered for log persistence test (marker: {marker ?? "<none>"})");
                });
            }

            await app.RunAsync();
        }

        /// <summary>
        /// Merges <c>Cors:AllowedOrigins</c> with optional comma-separated <c>Cors:ExtraAllowedOrigins</c>
        /// (environment variable <c>Cors__ExtraAllowedOrigins</c>) so deploy hosts can append origins without indexed keys.
        /// </summary>
        private static string[] BuildCorsAllowedOrigins(IConfiguration configuration)
        {
            var defaults = new[]
            {
                "http://localhost:3000",
                "http://localhost:4200",
                "https://localhost:3000",
                "https://localhost:4200"
            };

            var baseOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? defaults;

            var extra = configuration["Cors:ExtraAllowedOrigins"];
            if (string.IsNullOrWhiteSpace(extra))
                return baseOrigins;

            var extras = extra.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return baseOrigins
                .Concat(extras)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// In Docker (e.g. Coolify), <c>localhost</c> is the API container — PostgreSQL runs in another container.
        /// </summary>
        private static void ValidateProductionDatabaseHost(IConfiguration configuration, IHostEnvironment environment)
        {
            if (!environment.IsProduction()) return;
            var raw = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(raw)) return;
            try
            {
                var csb = new Npgsql.NpgsqlConnectionStringBuilder(raw);
                if (IsLoopbackDatabaseHost(csb.Host))
                {
                    throw new InvalidOperationException(
                        "Production DefaultConnection must not use Host=localhost or 127.0.0.1. Inside Docker that is this container, not PostgreSQL. " +
                        "Set ConnectionStrings__DefaultConnection in Coolify to the internal PostgreSQL hostname (same Docker network as the API) and Port=5432.");
                }
            }
            catch (ArgumentException)
            {
                // Malformed string; let a later failure surface the error.
            }
        }

        private static bool IsLoopbackDatabaseHost(string? host) =>
            host is null ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            host.Equals("::1", StringComparison.Ordinal);

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

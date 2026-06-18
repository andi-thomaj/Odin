using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Odin.Api.Authentication;
using Odin.Api.Extensions;
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
using Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement;
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
            // Policy definitions live in RateLimitingExtensions; UseRateLimiter() is wired into the
            // pipeline AFTER UseAuthentication() below so per-user tiers/partitioning see context.User.
            services.AddOdinRateLimiting();

            // Abstracts the system clock so time-based logic (e.g. merge-bundle retention) is unit-
            // testable with a fake clock instead of the ambient DateTime.UtcNow. New/edited code should
            // prefer injecting TimeProvider; existing DateTime.UtcNow sites can migrate incrementally.
            services.AddSingleton(TimeProvider.System);

            // ── Health Checks ───────────────────────────────────────────
            // Postgres is the hard readiness gate (Unhealthy → 503 → pulled from the LB). Hangfire
            // storage and tools-api reachability are reported as Degraded only: they're visible in the
            // health payload but don't take the whole API out of rotation, since most endpoints don't
            // depend on them.
            services.AddHttpClient(Odin.Api.HealthChecks.ToolsApiHealthCheck.HttpClientName, client =>
                client.Timeout = TimeSpan.FromSeconds(5));
            services.AddHealthChecks()
                .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!)
                .AddCheck<Odin.Api.HealthChecks.HangfireStorageHealthCheck>(
                    "hangfire",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                    tags: ["ready"])
                .AddCheck<Odin.Api.HealthChecks.ToolsApiHealthCheck>(
                    "tools-api",
                    failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                    tags: ["ready"]);

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
                            "X-App", // identifies the calling application (multi-app data isolation)
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
                // Admin AADR-panel restore: multi-GB uploads over a possibly-slow link. Bounds the
                // whole stream-through (.NET -> tools-api); see MergePanelAdminEndpoints.
                options.AddPolicy("PanelRestore", TimeSpan.FromHours(2));
            });

            // Not pooled: the context constructor-injects the scoped IAppContext that drives the multi-app
            // query filters + write stamping, which AddDbContextPool forbids (pooled instances take only
            // options). This API runs as a single, non-horizontally-scaled instance, so the pooling cost is
            // negligible. The ConfigureWarnings silences a benign EF warning raised because app-scoped entities
            // reference unfiltered shared tables (e.g. Calculator→AdmixToolsEra) — those reference rows always
            // exist, so the required-navigation/query-filter interaction is harmless here.
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                        npgsqlOptions => npgsqlOptions.CommandTimeout(180))
                    .ConfigureWarnings(w => w.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
                            .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
            services.AddScoped<ApplicationDbContextInitializer>();
            services.AddScoped<DatabaseSeeder>();

            // Multi-app data isolation: one RequestAppContext per request (set by AppResolutionMiddleware from
            // the X-App header), exposed read-only as IAppContext to the DbContext + auth hot path. The registry
            // validates the header and carries per-app branding.
            services.AddScoped<Odin.Api.Authentication.RequestAppContext>();
            services.AddScoped<Odin.Api.Authentication.IAppContext>(
                sp => sp.GetRequiredService<Odin.Api.Authentication.RequestAppContext>());
            services.AddScoped<Odin.Api.Services.IApplicationRegistry, Odin.Api.Services.ApplicationRegistry>();

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
            services.AddScoped<Odin.Api.Hubs.IGeneticInspectionRealtimeNotifier, Odin.Api.Hubs.GeneticInspectionRealtimeNotifier>();
            services.AddScoped<IAppSettingsService, AppSettingsService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IMediaService, MediaService>();
            services.AddScoped<IG25PopulationSampleService, G25PopulationSampleService>();
            services.AddScoped<IG25DistancePopulationSampleService, G25DistancePopulationSampleService>();
            services.AddScoped<IG25PcaPopulationsSampleService, G25PcaPopulationsSampleService>();
            services.AddScoped<IQpadmPopulationSampleService, QpadmPopulationSampleService>();
            services.AddScoped<IQpadmPopulationPanelSampleService, QpadmPopulationPanelSampleService>();
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
            services.AddScoped<IBackendCacheMaintenanceService, BackendCacheMaintenanceService>();
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
            // Merges are pinned to STRICTLY ONE AT A TIME and run sequentially — a deliberate policy
            // (the tools-api merges with trident at ~1.3 GB, so this is no longer a RAM necessity). The
            // in-flight cap is hardcoded to 1 here (NOT bound from `Merge:MaxConcurrentMerges`) so no
            // appsettings/env override can let two merges run concurrently.
            services.Configure<Odin.Api.Configuration.MergeJobOptions>(
                opts => opts.MaxConcurrentMerges = 1);
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
            })
            .ConfigurePrimaryHttpMessageHandler(CreateToolsApiHandler);
            // Background Y-DNA clade computation for qpAdm orders (enqueued via Hangfire at order time).
            services.AddScoped<
                Odin.Api.Endpoints.CladeFinderManagement.IYHaplogroupComputeService,
                Odin.Api.Endpoints.CladeFinderManagement.YHaplogroupComputeService>();

            // Y-haplogroup heatmap: typed client over the tools-api export, the rerunnable import job,
            // and the per-clade distribution reader. Same tools-api base URL/key/handler as the clade finder.
            services.AddHttpClient<
                Odin.Api.Endpoints.HaplogroupHeatmap.IHaploGeoExportClient,
                Odin.Api.Endpoints.HaplogroupHeatmap.HaploGeoExportClient>((sp, client) =>
            {
                var toolsOptions = sp.GetRequiredService<IOptions<Odin.Api.Configuration.ToolsApiOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(toolsOptions.BaseUrl))
                {
                    client.BaseAddress = new Uri(toolsOptions.BaseUrl);
                }
                // The export's first call parses + computes centroids (seconds); allow more than the default.
                client.Timeout = TimeSpan.FromSeconds(toolsOptions.MergeTimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(CreateToolsApiHandler);
            services.AddScoped<
                Odin.Api.Endpoints.HaplogroupHeatmap.IHaplogroupImportService,
                Odin.Api.Endpoints.HaplogroupHeatmap.HaplogroupImportService>();
            services.AddScoped<
                Odin.Api.Endpoints.HaplogroupHeatmap.IHaplogroupDistributionService,
                Odin.Api.Endpoints.HaplogroupHeatmap.HaplogroupDistributionService>();

            // Merge pipeline proxy (convert-to-23andMe + AADR merge). Its own HttpClient because the
            // merge call is long-running (minutes) — uses MergeTimeoutSeconds, not TimeoutSeconds.
            services.AddHttpClient<
                Odin.Api.Endpoints.MergeManagement.IMergePipelineService,
                Odin.Api.Endpoints.MergeManagement.MergePipelineService>((sp, client) =>
            {
                var toolsOptions = sp.GetRequiredService<IOptions<Odin.Api.Configuration.ToolsApiOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(toolsOptions.BaseUrl))
                {
                    client.BaseAddress = new Uri(toolsOptions.BaseUrl);
                }
                client.Timeout = TimeSpan.FromSeconds(toolsOptions.MergeTimeoutSeconds);
            })
            .ConfigurePrimaryHttpMessageHandler(CreateToolsApiHandler);

            // Dedicated client for the admin AADR-panel upload only. Infinite per-call timeout so a
            // multi-GB stream-through isn't killed by the 30-min merge client timeout; it's bounded
            // instead by the endpoint's "PanelRestore" RequestTimeout policy + request cancellation.
            services.AddHttpClient(
                Odin.Api.Endpoints.MergeManagement.MergePipelineService.PanelClientName, (sp, client) =>
            {
                var toolsOptions = sp.GetRequiredService<IOptions<Odin.Api.Configuration.ToolsApiOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(toolsOptions.BaseUrl))
                {
                    client.BaseAddress = new Uri(toolsOptions.BaseUrl);
                }
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(CreateToolsApiHandler);
            // Background convert+merge / delete jobs (enqueued via Hangfire onto the "merge" queue).
            services.AddScoped<
                Odin.Api.Endpoints.MergeManagement.IMergeJob,
                Odin.Api.Endpoints.MergeManagement.MergeJob>();

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
            // parameters". The storage stays cold (no connection opened) until the first job
            // is enqueued or the server starts — but the connection string must still PARSE
            // when the storage is built. Order creation enqueues a Y-DNA compute job, so even
            // in tests IBackgroundJobClient gets resolved and the storage gets built; read the
            // connection string from the live configuration (not the outer `connectionString`
            // captured before WebApplicationFactory's ConfigureAppConfiguration overrides apply)
            // so the test host's container connection string is honored — same reason the
            // Serilog sink above re-reads from context.Configuration.
            services.AddHangfire((provider, hf) => hf
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(
                    c => c.UseNpgsqlConnection(
                        provider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
                            ?? connectionString),
                    new PostgreSqlStorageOptions
                    {
                        // Heartbeat-extend a running job's lease instead of using a fixed visibility window.
                        // The AADR merge is disk-bound and can run >30 min on the shared host; with the
                        // DEFAULT fixed 30-min InvisibilityTimeout, Hangfire decided the still-running merge
                        // job was "lost" and re-fetched + re-ran it every 30 min — a perpetual loop that
                        // never reached Succeeded/Failed (each re-run kicked off a fresh tools-api forge,
                        // which again ran >30 min, re-fetched again, forever). Sliding timeout keeps the
                        // lease alive while the worker is genuinely processing, so a long merge runs exactly
                        // once. InvisibilityTimeout below is now only the ceiling for a truly dead worker.
                        UseSlidingInvisibilityTimeout = true,
                        InvisibilityTimeout = TimeSpan.FromHours(2),
                    })
                // Flip a merge order Retrying → Failed once Hangfire exhausts its retries (see
                // MergeJobFailureStateFilter), so a dead job doesn't hold an in-flight merge slot forever.
                .UseFilter(new Odin.Api.Endpoints.MergeManagement.MergeJobFailureStateFilter(
                    provider.GetRequiredService<IServiceScopeFactory>())));

            // Worker process — skipped in Testing so the suite doesn't poll the throwaway
            // Postgres container or compete with Respawn for table locks.
            if (!builder.Environment.IsEnvironment("Testing"))
            {
                // Default server: everything except the "merge" queue (Y-DNA compute, cleanup, ...).
                services.AddHangfireServer(opts =>
                {
                    opts.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
                    opts.Queues = new[] { "default" };
                });

                // Dedicated server for the "merge" queue with a SINGLE worker, so merges execute
                // strictly sequentially — one finishes before the next starts (a deliberate policy; the
                // tools-api merges with trident at ~1.3 GB, so it's no longer a RAM necessity).
                // WorkerCount is a hard literal 1 (not derived from config) to match the pinned in-flight
                // cap above. MergeJob.RunAsync is [Queue("merge")].
                services.AddHangfireServer(opts =>
                {
                    opts.ServerName = "merge-worker";
                    opts.WorkerCount = 1;
                    opts.Queues = new[] { "merge" };
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
            // Resolve the calling app (X-App header) into IAppContext BEFORE role enrichment and any DB access,
            // so provisioning/role lookup and the app-scoped query filters key on the right application.
            app.UseAppResolution();
            // Rate limiting must run AFTER authentication: the global limiter's authenticated tier
            // (100 vs 30 req/min) and per-user partitioning both key off context.User, which is only
            // populated once UseAuthentication has run. Placed before role enrichment so a throttled
            // request is rejected before the (heavier) Auth0 role/email-verified lookup runs.
            if (!app.Environment.IsEnvironment("Testing"))
                app.UseRateLimiter();
            app.UseRoleEnrichment();
            app.UseAuthorization();

            app.MapHub<NotificationHub>("/hubs/notifications");
            app.MapHealthChecks("/health");

            // Hangfire dashboard — mounted in every environment, gated to Admin users by
            // HangfireDashboardAuthFilter (admins reach it via the SPA "Open Hangfire dashboard" button,
            // which mints the /jobs-scoped cookie at /v1/api/admin/hangfire/session). A direct, cookie-less
            // navigation to /jobs returns 401 by design — that is the admin gate, not a routing failure.
            app.UseHangfireDashboard("/jobs", new DashboardOptions
            {
                Authorization = new[] { new HangfireDashboardAuthFilter() },
                DashboardTitle = "Odin background jobs"
            });

            // Recurring jobs need a running Hangfire server, which is skipped in Testing — so register them
            // only outside Testing (the dashboard above only reads storage and is safe to mount everywhere).
            if (!app.Environment.IsEnvironment("Testing"))
            {
                // Registered after the Hangfire storage is wired (see AddHangfire above).
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

                // merge-dispatch: bounded-queue admission. Every minute, admit waiting (NotStarted) qpAdm
                // merges up to the in-flight cap (2). Backstop for the dispatch passes triggered on order
                // creation and merge completion — also the only thing that refills capacity if those are
                // missed. Runs on the default queue (it only enqueues to the "merge" queue, never merges).
                recurringJobManager.AddOrUpdate<Odin.Api.Endpoints.MergeManagement.IMergeJob>(
                    recurringJobId: "merge-dispatch",
                    methodCall: svc => svc.DispatchPendingMergesAsync(CancellationToken.None),
                    cronExpression: "* * * * *",
                    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

                // merge-cleanup: reclaim orphaned AADR merge bundles (completed orders, or past the
                // retention window) once a week. Safety net for inline deletes that never fired; keeps the
                // 80 GB disk bounded. CleanupOrphansAsync is [Queue("merge")] so it can't race a live merge.
                // The weekly cron is pinned to the deploy moment's weekday/hour/minute (UTC) so the first
                // run lands one week after deployment rather than on a fixed calendar boundary — Hangfire
                // never fires a cron job on registration, so the next occurrence of "this same weekday and
                // time" is ~7 days out. Each deploy re-anchors the schedule to one week from that deploy.
                var deployUtc = DateTime.UtcNow;
                var weeklyFromDeployCron = $"{deployUtc.Minute} {deployUtc.Hour} * * {(int)deployUtc.DayOfWeek}";
                recurringJobManager.AddOrUpdate<Odin.Api.Endpoints.MergeManagement.IMergeJob>(
                    recurringJobId: "merge-cleanup",
                    methodCall: svc => svc.CleanupOrphansAsync(CancellationToken.None),
                    cronExpression: weeklyFromDeployCron,
                    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
            }

            // Version prefix — all business endpoints live under /v1 so breaking changes
            // can ship a v2 alongside without breaking existing clients. SignalR hubs and
            // health checks stay at the root by convention (not part of the API surface).
            var v1 = app.MapGroup("/v1");
            v1.MapOdinV1Endpoints();

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
        /// Primary handler for both tools-api clients: a SocketsHttpHandler with a short ConnectTimeout
        /// so an unreachable odin-tools-api fails fast (≈ConnectTimeout) instead of hanging for the long
        /// overall request timeout (clade 300s / merge 1800s). PooledConnectionLifetime keeps DNS fresh.
        /// </summary>
        private static HttpMessageHandler CreateToolsApiHandler(IServiceProvider sp)
        {
            var opts = sp.GetRequiredService<IOptions<Odin.Api.Configuration.ToolsApiOptions>>().Value;
            return new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(opts.ConnectTimeoutSeconds),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };
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

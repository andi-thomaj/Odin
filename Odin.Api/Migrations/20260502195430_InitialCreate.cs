using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "application_users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdentityId = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "User"),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "g25_admixture_eras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_admixture_eras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "g25_continents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_continents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "g25_distance_eras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_distance_eras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "g25_orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    HasViewedResults = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ExpeditedProcessing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Message = table.Column<string>(type: "text", nullable: false),
                    MessageTemplate = table.Column<string>(type: "text", nullable: true),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "music_tracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_music_tracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "paddle_customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaddleCustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MarketingConsent = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PaddleCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaddleUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paddle_customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "paddle_notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaddleNotificationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PaddleEventId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProcessingError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProcessingAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paddle_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "paddle_products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaddleProductId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TaxCategory = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ServiceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ParentServiceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AddonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true),
                    PaddleCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaddleUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paddle_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "paddle_subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaddleSubscriptionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PaddleCustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CollectionMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstBilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextBilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PausedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CanceledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledChangeAction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ScheduledChangeEffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true),
                    PaddleCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaddleUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paddle_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "paddle_transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaddleTransactionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PaddleCustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PaddleSubscriptionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InvoiceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CollectionMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Subtotal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TaxTotal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DiscountTotal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    GrandTotal = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaddleCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaddleUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paddle_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_eras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_eras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_ethnicities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_ethnicities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    HasViewedResults = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ExpeditedProcessing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IncludesYHaplogroup = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IncludesRawMerge = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AddonsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_population_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_population_samples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "raw_genetic_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawData = table.Column<byte[]>(type: "bytea", nullable: false),
                    RawDataFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MergedRawData = table.Column<byte[]>(type: "bytea", nullable: true),
                    MergedRawDataFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_genetic_files", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admixture_saved_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "source"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admixture_saved_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admixture_saved_files_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_saved_coordinates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RawInput = table.Column<string>(type: "text", nullable: false),
                    Scaling = table.Column<bool>(type: "boolean", nullable: false),
                    AddMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CustomName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ViewId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_saved_coordinates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_saved_coordinates_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipientUserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_application_users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    AdminNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: true),
                    FileContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_admixture_population_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    Ids = table.Column<string>(type: "text", nullable: false),
                    G25AdmixtureEraId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_admixture_population_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_admixture_population_samples_g25_admixture_eras_G25Admi~",
                        column: x => x.G25AdmixtureEraId,
                        principalTable: "g25_admixture_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "g25_ethnicities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    G25ContinentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_ethnicities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_ethnicities_g25_continents_G25ContinentId",
                        column: x => x.G25ContinentId,
                        principalTable: "g25_continents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_distance_population_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    Ids = table.Column<string>(type: "text", nullable: false),
                    G25DistanceEraId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_distance_population_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_distance_population_samples_g25_distance_eras_G25Distan~",
                        column: x => x.G25DistanceEraId,
                        principalTable: "g25_distance_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "g25_pca_populations_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    Ids = table.Column<string>(type: "text", nullable: false),
                    G25DistanceEraId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_pca_populations_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_pca_populations_samples_g25_distance_eras_G25DistanceEr~",
                        column: x => x.G25DistanceEraId,
                        principalTable: "g25_distance_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "music_track_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MusicTrackId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_music_track_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_music_track_files_music_tracks_MusicTrackId",
                        column: x => x.MusicTrackId,
                        principalTable: "music_tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paddle_prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaddlePriceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PaddleProductInternalId = table.Column<int>(type: "integer", nullable: false),
                    PaddleProductId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    UnitPriceAmount = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UnitPriceCurrency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    BillingCycleInterval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    BillingCycleFrequency = table.Column<int>(type: "integer", nullable: true),
                    TrialPeriodInterval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    TrialPeriodFrequency = table.Column<int>(type: "integer", nullable: true),
                    TaxMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CustomData = table.Column<string>(type: "jsonb", nullable: true),
                    PaddleCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaddleUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paddle_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paddle_prices_paddle_products_PaddleProductInternalId",
                        column: x => x.PaddleProductInternalId,
                        principalTable: "paddle_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_populations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    GeoJson = table.Column<string>(type: "text", nullable: false),
                    IconFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    EraId = table.Column<int>(type: "integer", nullable: false),
                    MusicTrackId = table.Column<int>(type: "integer", nullable: false),
                    VideoAvatarVersion = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_populations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_populations_music_tracks_MusicTrackId",
                        column: x => x.MusicTrackId,
                        principalTable: "music_tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_populations_qpadm_eras_EraId",
                        column: x => x.EraId,
                        principalTable: "qpadm_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EthnicityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_regions_qpadm_ethnicities_EthnicityId",
                        column: x => x.EthnicityId,
                        principalTable: "qpadm_ethnicities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paddle_payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaddleTransactionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ReceiptUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paddle_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paddle_payments_qpadm_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "qpadm_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    G25Coordinates = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RawGeneticFileId = table.Column<int>(type: "integer", nullable: false),
                    ProfilePicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    ProfilePictureFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspections_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspections_g25_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "g25_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspections_raw_genetic_files_RawGeneticFileId",
                        column: x => x.RawGeneticFileId,
                        principalTable: "raw_genetic_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_genetic_inspections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RawGeneticFileId = table.Column<int>(type: "integer", nullable: false),
                    ProfilePicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    ProfilePictureFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_genetic_inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_genetic_inspections_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_genetic_inspections_qpadm_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "qpadm_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_genetic_inspections_raw_genetic_files_RawGeneticFileId",
                        column: x => x.RawGeneticFileId,
                        principalTable: "raw_genetic_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    G25EthnicityId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_regions_g25_ethnicities_G25EthnicityId",
                        column: x => x.G25EthnicityId,
                        principalTable: "g25_ethnicities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "research_links",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Link = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    G25AdmixturePopulationSampleId = table.Column<int>(type: "integer", nullable: true),
                    G25DistancePopulationSampleId = table.Column<int>(type: "integer", nullable: true),
                    G25PcaPopulationsSampleId = table.Column<int>(type: "integer", nullable: true),
                    QpadmPopulationSampleId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_research_links_g25_admixture_population_samples_G25Admixtur~",
                        column: x => x.G25AdmixturePopulationSampleId,
                        principalTable: "g25_admixture_population_samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_research_links_g25_distance_population_samples_G25DistanceP~",
                        column: x => x.G25DistancePopulationSampleId,
                        principalTable: "g25_distance_population_samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_research_links_g25_pca_populations_samples_G25PcaPopulation~",
                        column: x => x.G25PcaPopulationsSampleId,
                        principalTable: "g25_pca_populations_samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_research_links_qpadm_population_samples_QpadmPopulationSamp~",
                        column: x => x.QpadmPopulationSampleId,
                        principalTable: "qpadm_population_samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_admixture_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    FitDistance = table.Column<double>(type: "double precision", precision: 18, scale: 10, nullable: false),
                    ResultsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    Ancestors = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_admixture_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_admixture_results_g25_genetic_inspections_GeneticInspec~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_distance_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25DistanceEraId = table.Column<int>(type: "integer", nullable: false),
                    ResultsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    Populations = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_distance_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_distance_results_g25_distance_eras_G25DistanceEraId",
                        column: x => x.G25DistanceEraId,
                        principalTable: "g25_distance_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_distance_results_g25_genetic_inspections_GeneticInspect~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspection_continents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25ContinentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspection_continents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_continents_g25_continents_G25Contine~",
                        column: x => x.G25ContinentId,
                        principalTable: "g25_continents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_continents_g25_genetic_inspections_G~",
                        column: x => x.G25GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspection_ethnicities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25EthnicityId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspection_ethnicities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_ethnicities_g25_ethnicities_G25Ethni~",
                        column: x => x.G25EthnicityId,
                        principalTable: "g25_ethnicities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_ethnicities_g25_genetic_inspections_~",
                        column: x => x.G25GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_pca_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25ContinentId = table.Column<int>(type: "integer", nullable: false),
                    ResultsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_pca_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_pca_results_g25_continents_G25ContinentId",
                        column: x => x.G25ContinentId,
                        principalTable: "g25_continents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_pca_results_g25_genetic_inspections_GeneticInspectionId",
                        column: x => x.GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_genetic_inspection_regions",
                columns: table => new
                {
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    RegionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_genetic_inspection_regions", x => new { x.GeneticInspectionId, x.RegionId });
                    table.ForeignKey(
                        name: "FK_qpadm_genetic_inspection_regions_qpadm_genetic_inspections_~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "qpadm_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_genetic_inspection_regions_qpadm_regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "qpadm_regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    ResultsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_results_qpadm_genetic_inspections_GeneticInspectionId",
                        column: x => x.GeneticInspectionId,
                        principalTable: "qpadm_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspection_regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25RegionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspection_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_regions_g25_genetic_inspections_G25G~",
                        column: x => x.G25GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_regions_g25_regions_G25RegionId",
                        column: x => x.G25RegionId,
                        principalTable: "g25_regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_result_era_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QpadmResultId = table.Column<int>(type: "integer", nullable: false),
                    EraId = table.Column<int>(type: "integer", nullable: false),
                    PValue = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    RightSources = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_result_era_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_result_era_groups_qpadm_eras_EraId",
                        column: x => x.EraId,
                        principalTable: "qpadm_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_result_era_groups_qpadm_results_QpadmResultId",
                        column: x => x.QpadmResultId,
                        principalTable: "qpadm_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qpadm_result_populations",
                columns: table => new
                {
                    QpadmResultEraGroupId = table.Column<int>(type: "integer", nullable: false),
                    PopulationId = table.Column<int>(type: "integer", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    StandardError = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ZScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_result_populations", x => new { x.QpadmResultEraGroupId, x.PopulationId });
                    table.ForeignKey(
                        name: "FK_qpadm_result_populations_qpadm_populations_PopulationId",
                        column: x => x.PopulationId,
                        principalTable: "qpadm_populations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_qpadm_result_populations_qpadm_result_era_groups_QpadmResul~",
                        column: x => x.QpadmResultEraGroupId,
                        principalTable: "qpadm_result_era_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admixture_saved_files_UserId_Kind_UpdatedAt",
                table: "admixture_saved_files",
                columns: new[] { "UserId", "Kind", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_app_settings_Key",
                table: "app_settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_eras_Name",
                table: "g25_admixture_eras",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_population_samples_G25AdmixtureEraId",
                table: "g25_admixture_population_samples",
                column: "G25AdmixtureEraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_population_samples_Label",
                table: "g25_admixture_population_samples",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_results_GeneticInspectionId",
                table: "g25_admixture_results",
                column: "GeneticInspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_continents_Name",
                table: "g25_continents",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_eras_Name",
                table: "g25_distance_eras",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_population_samples_G25DistanceEraId",
                table: "g25_distance_population_samples",
                column: "G25DistanceEraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_population_samples_Label",
                table: "g25_distance_population_samples",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_results_G25DistanceEraId",
                table: "g25_distance_results",
                column: "G25DistanceEraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_results_GeneticInspectionId_G25DistanceEraId",
                table: "g25_distance_results",
                columns: new[] { "GeneticInspectionId", "G25DistanceEraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_ethnicities_G25ContinentId",
                table: "g25_ethnicities",
                column: "G25ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_ethnicities_Name",
                table: "g25_ethnicities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_continents_G25ContinentId",
                table: "g25_genetic_inspection_continents",
                column: "G25ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_continents_G25GeneticInspectionId_G2~",
                table: "g25_genetic_inspection_continents",
                columns: new[] { "G25GeneticInspectionId", "G25ContinentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_ethnicities_G25EthnicityId",
                table: "g25_genetic_inspection_ethnicities",
                column: "G25EthnicityId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_ethnicities_G25GeneticInspectionId_G~",
                table: "g25_genetic_inspection_ethnicities",
                columns: new[] { "G25GeneticInspectionId", "G25EthnicityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_regions_G25GeneticInspectionId_G25Re~",
                table: "g25_genetic_inspection_regions",
                columns: new[] { "G25GeneticInspectionId", "G25RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_regions_G25RegionId",
                table: "g25_genetic_inspection_regions",
                column: "G25RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspections_OrderId",
                table: "g25_genetic_inspections",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspections_RawGeneticFileId",
                table: "g25_genetic_inspections",
                column: "RawGeneticFileId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspections_UserId",
                table: "g25_genetic_inspections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_populations_samples_G25DistanceEraId",
                table: "g25_pca_populations_samples",
                column: "G25DistanceEraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_populations_samples_Label",
                table: "g25_pca_populations_samples",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_results_G25ContinentId",
                table: "g25_pca_results",
                column: "G25ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_results_GeneticInspectionId_G25ContinentId",
                table: "g25_pca_results",
                columns: new[] { "GeneticInspectionId", "G25ContinentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_regions_G25EthnicityId_Name",
                table: "g25_regions",
                columns: new[] { "G25EthnicityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_saved_coordinates_UserId_UpdatedAt",
                table: "g25_saved_coordinates",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_logs_Timestamp",
                table: "logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_music_track_files_MusicTrackId",
                table: "music_track_files",
                column: "MusicTrackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RecipientUserId_IsRead",
                table: "notifications",
                columns: new[] { "RecipientUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_paddle_customers_Email",
                table: "paddle_customers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_customers_PaddleCustomerId",
                table: "paddle_customers",
                column: "PaddleCustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paddle_customers_UserId",
                table: "paddle_customers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_notifications_EventType",
                table: "paddle_notifications",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_notifications_PaddleNotificationId",
                table: "paddle_notifications",
                column: "PaddleNotificationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paddle_notifications_ProcessedStatus",
                table: "paddle_notifications",
                column: "ProcessedStatus");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_payments_OrderId",
                table: "paddle_payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_payments_PaddleTransactionId",
                table: "paddle_payments",
                column: "PaddleTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paddle_payments_UserId",
                table: "paddle_payments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_prices_PaddlePriceId",
                table: "paddle_prices",
                column: "PaddlePriceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paddle_prices_PaddleProductId",
                table: "paddle_prices",
                column: "PaddleProductId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_prices_PaddleProductInternalId",
                table: "paddle_prices",
                column: "PaddleProductInternalId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_products_AddonCode",
                table: "paddle_products",
                column: "AddonCode");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_products_Kind",
                table: "paddle_products",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_products_PaddleProductId",
                table: "paddle_products",
                column: "PaddleProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paddle_products_ParentServiceType",
                table: "paddle_products",
                column: "ParentServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_products_ServiceType",
                table: "paddle_products",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_subscriptions_PaddleCustomerId",
                table: "paddle_subscriptions",
                column: "PaddleCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_subscriptions_PaddleSubscriptionId",
                table: "paddle_subscriptions",
                column: "PaddleSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paddle_subscriptions_Status",
                table: "paddle_subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_transactions_PaddleCustomerId",
                table: "paddle_transactions",
                column: "PaddleCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_transactions_PaddleSubscriptionId",
                table: "paddle_transactions",
                column: "PaddleSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_paddle_transactions_PaddleTransactionId",
                table: "paddle_transactions",
                column: "PaddleTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paddle_transactions_Status",
                table: "paddle_transactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_genetic_inspection_regions_RegionId",
                table: "qpadm_genetic_inspection_regions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_genetic_inspections_OrderId",
                table: "qpadm_genetic_inspections",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_genetic_inspections_RawGeneticFileId",
                table: "qpadm_genetic_inspections",
                column: "RawGeneticFileId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_genetic_inspections_UserId",
                table: "qpadm_genetic_inspections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_population_samples_Label",
                table: "qpadm_population_samples",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_populations_EraId",
                table: "qpadm_populations",
                column: "EraId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_populations_MusicTrackId",
                table: "qpadm_populations",
                column: "MusicTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_regions_EthnicityId",
                table: "qpadm_regions",
                column: "EthnicityId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_era_groups_EraId",
                table: "qpadm_result_era_groups",
                column: "EraId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_era_groups_QpadmResultId",
                table: "qpadm_result_era_groups",
                column: "QpadmResultId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_result_populations_PopulationId",
                table: "qpadm_result_populations",
                column: "PopulationId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_results_GeneticInspectionId",
                table: "qpadm_results",
                column: "GeneticInspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_raw_genetic_files_CreatedBy_RawDataFileName",
                table: "raw_genetic_files",
                columns: new[] { "CreatedBy", "RawDataFileName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_reports_UserId_Status",
                table: "reports",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_research_links_G25AdmixturePopulationSampleId",
                table: "research_links",
                column: "G25AdmixturePopulationSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_research_links_G25DistancePopulationSampleId",
                table: "research_links",
                column: "G25DistancePopulationSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_research_links_G25PcaPopulationsSampleId",
                table: "research_links",
                column: "G25PcaPopulationsSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_research_links_QpadmPopulationSampleId",
                table: "research_links",
                column: "QpadmPopulationSampleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admixture_saved_files");

            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "g25_admixture_results");

            migrationBuilder.DropTable(
                name: "g25_distance_results");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspection_continents");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspection_ethnicities");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspection_regions");

            migrationBuilder.DropTable(
                name: "g25_pca_results");

            migrationBuilder.DropTable(
                name: "g25_saved_coordinates");

            migrationBuilder.DropTable(
                name: "logs");

            migrationBuilder.DropTable(
                name: "music_track_files");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "paddle_customers");

            migrationBuilder.DropTable(
                name: "paddle_notifications");

            migrationBuilder.DropTable(
                name: "paddle_payments");

            migrationBuilder.DropTable(
                name: "paddle_prices");

            migrationBuilder.DropTable(
                name: "paddle_subscriptions");

            migrationBuilder.DropTable(
                name: "paddle_transactions");

            migrationBuilder.DropTable(
                name: "qpadm_genetic_inspection_regions");

            migrationBuilder.DropTable(
                name: "qpadm_result_populations");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "research_links");

            migrationBuilder.DropTable(
                name: "g25_regions");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspections");

            migrationBuilder.DropTable(
                name: "paddle_products");

            migrationBuilder.DropTable(
                name: "qpadm_regions");

            migrationBuilder.DropTable(
                name: "qpadm_populations");

            migrationBuilder.DropTable(
                name: "qpadm_result_era_groups");

            migrationBuilder.DropTable(
                name: "g25_admixture_population_samples");

            migrationBuilder.DropTable(
                name: "g25_distance_population_samples");

            migrationBuilder.DropTable(
                name: "g25_pca_populations_samples");

            migrationBuilder.DropTable(
                name: "qpadm_population_samples");

            migrationBuilder.DropTable(
                name: "g25_ethnicities");

            migrationBuilder.DropTable(
                name: "g25_orders");

            migrationBuilder.DropTable(
                name: "qpadm_ethnicities");

            migrationBuilder.DropTable(
                name: "music_tracks");

            migrationBuilder.DropTable(
                name: "qpadm_eras");

            migrationBuilder.DropTable(
                name: "qpadm_results");

            migrationBuilder.DropTable(
                name: "g25_admixture_eras");

            migrationBuilder.DropTable(
                name: "g25_distance_eras");

            migrationBuilder.DropTable(
                name: "g25_continents");

            migrationBuilder.DropTable(
                name: "qpadm_genetic_inspections");

            migrationBuilder.DropTable(
                name: "application_users");

            migrationBuilder.DropTable(
                name: "qpadm_orders");

            migrationBuilder.DropTable(
                name: "raw_genetic_files");
        }
    }
}

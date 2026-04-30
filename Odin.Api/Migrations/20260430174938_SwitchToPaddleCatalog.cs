using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToPaddleCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_qpadm_orders_promo_codes_PromoCodeId",
                table: "qpadm_orders");

            migrationBuilder.DropTable(
                name: "catalog_product_addons");

            migrationBuilder.DropTable(
                name: "order_line_addons");

            migrationBuilder.DropTable(
                name: "promo_codes");

            migrationBuilder.DropTable(
                name: "catalog_products");

            migrationBuilder.DropTable(
                name: "product_addons");

            migrationBuilder.DropIndex(
                name: "IX_qpadm_orders_PromoCodeId",
                table: "qpadm_orders");

            migrationBuilder.DropColumn(
                name: "PromoCodeId",
                table: "qpadm_orders");

            migrationBuilder.AddColumn<string>(
                name: "AddonsJson",
                table: "qpadm_orders",
                type: "jsonb",
                nullable: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paddle_customers");

            migrationBuilder.DropTable(
                name: "paddle_notifications");

            migrationBuilder.DropTable(
                name: "paddle_prices");

            migrationBuilder.DropTable(
                name: "paddle_subscriptions");

            migrationBuilder.DropTable(
                name: "paddle_transactions");

            migrationBuilder.DropTable(
                name: "paddle_products");

            migrationBuilder.DropColumn(
                name: "AddonsJson",
                table: "qpadm_orders");

            migrationBuilder.AddColumn<int>(
                name: "PromoCodeId",
                table: "qpadm_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalog_products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ServiceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_addons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_addons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "promo_codes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicableService = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MaxRedemptions = table.Column<int>(type: "integer", nullable: true),
                    RedemptionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ValidFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_addons",
                columns: table => new
                {
                    CatalogProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductAddonId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_product_addons", x => new { x.CatalogProductId, x.ProductAddonId });
                    table.ForeignKey(
                        name: "FK_catalog_product_addons_catalog_products_CatalogProductId",
                        column: x => x.CatalogProductId,
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_product_addons_product_addons_ProductAddonId",
                        column: x => x.ProductAddonId,
                        principalTable: "product_addons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_line_addons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    ProductAddonId = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_line_addons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_line_addons_product_addons_ProductAddonId",
                        column: x => x.ProductAddonId,
                        principalTable: "product_addons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_line_addons_qpadm_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "qpadm_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_orders_PromoCodeId",
                table: "qpadm_orders",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_addons_ProductAddonId",
                table: "catalog_product_addons",
                column: "ProductAddonId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_products_ServiceType",
                table: "catalog_products",
                column: "ServiceType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_line_addons_OrderId",
                table: "order_line_addons",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_order_line_addons_ProductAddonId",
                table: "order_line_addons",
                column: "ProductAddonId");

            migrationBuilder.CreateIndex(
                name: "IX_product_addons_Code",
                table: "product_addons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_Code",
                table: "promo_codes",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_qpadm_orders_promo_codes_PromoCodeId",
                table: "qpadm_orders",
                column: "PromoCodeId",
                principalTable: "promo_codes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

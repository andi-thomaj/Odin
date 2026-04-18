using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrderToQpadmOrderAddG25Order : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_genetic_inspections_orders_OrderId",
                table: "g25_genetic_inspections");

            migrationBuilder.DropColumn(
                name: "Service",
                table: "orders");

            migrationBuilder.RenameTable(
                name: "orders",
                newName: "qpadm_orders");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_orders"" RENAME TO ""PK_qpadm_orders"";");

            migrationBuilder.RenameIndex(
                name: "IX_orders_PromoCodeId",
                table: "qpadm_orders",
                newName: "IX_qpadm_orders_PromoCodeId");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_orders"" RENAME CONSTRAINT ""FK_orders_promo_codes_PromoCodeId"" TO ""FK_qpadm_orders_promo_codes_PromoCodeId"";");

            migrationBuilder.Sql(@"ALTER TABLE ""order_line_addons"" RENAME CONSTRAINT ""FK_order_line_addons_orders_OrderId"" TO ""FK_order_line_addons_qpadm_orders_OrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""paddle_payments"" RENAME CONSTRAINT ""FK_paddle_payments_orders_OrderId"" TO ""FK_paddle_payments_qpadm_orders_OrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_qpadm_genetic_inspections_orders_OrderId"" TO ""FK_qpadm_genetic_inspections_qpadm_orders_OrderId"";");

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

            migrationBuilder.AddForeignKey(
                name: "FK_g25_genetic_inspections_g25_orders_OrderId",
                table: "g25_genetic_inspections",
                column: "OrderId",
                principalTable: "g25_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_genetic_inspections_g25_orders_OrderId",
                table: "g25_genetic_inspections");

            migrationBuilder.DropTable(
                name: "g25_orders");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_genetic_inspections"" RENAME CONSTRAINT ""FK_qpadm_genetic_inspections_qpadm_orders_OrderId"" TO ""FK_qpadm_genetic_inspections_orders_OrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""paddle_payments"" RENAME CONSTRAINT ""FK_paddle_payments_qpadm_orders_OrderId"" TO ""FK_paddle_payments_orders_OrderId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""order_line_addons"" RENAME CONSTRAINT ""FK_order_line_addons_qpadm_orders_OrderId"" TO ""FK_order_line_addons_orders_OrderId"";");

            migrationBuilder.Sql(@"ALTER TABLE ""qpadm_orders"" RENAME CONSTRAINT ""FK_qpadm_orders_promo_codes_PromoCodeId"" TO ""FK_orders_promo_codes_PromoCodeId"";");

            migrationBuilder.RenameIndex(
                name: "IX_qpadm_orders_PromoCodeId",
                table: "qpadm_orders",
                newName: "IX_orders_PromoCodeId");

            migrationBuilder.Sql(@"ALTER INDEX ""PK_qpadm_orders"" RENAME TO ""PK_orders"";");

            migrationBuilder.RenameTable(
                name: "qpadm_orders",
                newName: "orders");

            migrationBuilder.AddColumn<string>(
                name: "Service",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "qpAdm");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_genetic_inspections_orders_OrderId",
                table: "g25_genetic_inspections",
                column: "OrderId",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections");

            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "genetic_inspections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Service = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspections_OrderId",
                table: "genetic_inspections",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections",
                column: "RawGeneticFileId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_genetic_inspections_orders_OrderId",
                table: "genetic_inspections",
                column: "OrderId",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_genetic_inspections_orders_OrderId",
                table: "genetic_inspections");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropIndex(
                name: "IX_genetic_inspections_OrderId",
                table: "genetic_inspections");

            migrationBuilder.DropIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "genetic_inspections");

            migrationBuilder.CreateIndex(
                name: "IX_genetic_inspections_RawGeneticFileId",
                table: "genetic_inspections",
                column: "RawGeneticFileId");
        }
    }
}

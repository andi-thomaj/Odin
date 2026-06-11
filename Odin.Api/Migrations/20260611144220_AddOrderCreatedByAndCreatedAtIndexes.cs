using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCreatedByAndCreatedAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_qpadm_orders_CreatedAt",
                table: "qpadm_orders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_orders_CreatedBy_CreatedAt",
                table: "qpadm_orders",
                columns: new[] { "CreatedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_g25_orders_CreatedAt",
                table: "g25_orders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_g25_orders_CreatedBy_CreatedAt",
                table: "g25_orders",
                columns: new[] { "CreatedBy", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_qpadm_orders_CreatedAt",
                table: "qpadm_orders");

            migrationBuilder.DropIndex(
                name: "IX_qpadm_orders_CreatedBy_CreatedAt",
                table: "qpadm_orders");

            migrationBuilder.DropIndex(
                name: "IX_g25_orders_CreatedAt",
                table: "g25_orders");

            migrationBuilder.DropIndex(
                name: "IX_g25_orders_CreatedBy_CreatedAt",
                table: "g25_orders");
        }
    }
}

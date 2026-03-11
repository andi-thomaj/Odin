using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveStatsToPopulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandardError",
                table: "qpadm_results");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "qpadm_results");

            migrationBuilder.DropColumn(
                name: "ZScore",
                table: "qpadm_results");

            migrationBuilder.AddColumn<decimal>(
                name: "StandardError",
                table: "qpadm_result_populations",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ZScore",
                table: "qpadm_result_populations",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandardError",
                table: "qpadm_result_populations");

            migrationBuilder.DropColumn(
                name: "ZScore",
                table: "qpadm_result_populations");

            migrationBuilder.AddColumn<decimal>(
                name: "StandardError",
                table: "qpadm_results",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "qpadm_results",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ZScore",
                table: "qpadm_results",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}

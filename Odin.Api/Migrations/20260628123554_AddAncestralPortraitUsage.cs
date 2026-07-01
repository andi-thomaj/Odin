using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAncestralPortraitUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "ancestral_portrait_sets",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageCount",
                table: "ancestral_portrait_sets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "UsageInputTokens",
                table: "ancestral_portrait_sets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UsageOutputTokens",
                table: "ancestral_portrait_sets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UsageTotalTokens",
                table: "ancestral_portrait_sets",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedCostUsd",
                table: "ancestral_portrait_sets");

            migrationBuilder.DropColumn(
                name: "ImageCount",
                table: "ancestral_portrait_sets");

            migrationBuilder.DropColumn(
                name: "UsageInputTokens",
                table: "ancestral_portrait_sets");

            migrationBuilder.DropColumn(
                name: "UsageOutputTokens",
                table: "ancestral_portrait_sets");

            migrationBuilder.DropColumn(
                name: "UsageTotalTokens",
                table: "ancestral_portrait_sets");
        }
    }
}

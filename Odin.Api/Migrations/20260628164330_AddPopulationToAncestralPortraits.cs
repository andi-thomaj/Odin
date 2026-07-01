using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPopulationToAncestralPortraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ancestral_portraits_SetId_EraId",
                table: "ancestral_portraits");

            migrationBuilder.AddColumn<int>(
                name: "PopulationId",
                table: "ancestral_portraits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Default 8 (not 0) so the existing singleton settings row gets a sensible cap — a 0 here would make the
            // generator Take(0) populations and produce nothing. Matches AncestralPortraitSettings.MaxPopulationsPerEra.
            migrationBuilder.AddColumn<int>(
                name: "MaxPopulationsPerEra",
                table: "ancestral_portrait_settings",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portraits_SetId_EraId_PopulationId",
                table: "ancestral_portraits",
                columns: new[] { "SetId", "EraId", "PopulationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ancestral_portraits_SetId_EraId_PopulationId",
                table: "ancestral_portraits");

            migrationBuilder.DropColumn(
                name: "PopulationId",
                table: "ancestral_portraits");

            migrationBuilder.DropColumn(
                name: "MaxPopulationsPerEra",
                table: "ancestral_portrait_settings");

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portraits_SetId_EraId",
                table: "ancestral_portraits",
                columns: new[] { "SetId", "EraId" });
        }
    }
}

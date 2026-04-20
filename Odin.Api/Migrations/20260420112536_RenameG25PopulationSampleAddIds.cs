using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameG25PopulationSampleAddIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_research_links_g25_population_samples_G25PopulationSampleId",
                table: "research_links");

            migrationBuilder.RenameColumn(
                name: "G25PopulationSampleId",
                table: "research_links",
                newName: "G25AdmixturePopulationSampleId");

            migrationBuilder.RenameIndex(
                name: "IX_research_links_G25PopulationSampleId",
                table: "research_links",
                newName: "IX_research_links_G25AdmixturePopulationSampleId");

            migrationBuilder.AddColumn<string>(
                name: "Ids",
                table: "g25_population_samples",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_research_links_g25_population_samples_G25AdmixturePopulatio~",
                table: "research_links",
                column: "G25AdmixturePopulationSampleId",
                principalTable: "g25_population_samples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_research_links_g25_population_samples_G25AdmixturePopulatio~",
                table: "research_links");

            migrationBuilder.DropColumn(
                name: "Ids",
                table: "g25_population_samples");

            migrationBuilder.RenameColumn(
                name: "G25AdmixturePopulationSampleId",
                table: "research_links",
                newName: "G25PopulationSampleId");

            migrationBuilder.RenameIndex(
                name: "IX_research_links_G25AdmixturePopulationSampleId",
                table: "research_links",
                newName: "IX_research_links_G25PopulationSampleId");

            migrationBuilder.AddForeignKey(
                name: "FK_research_links_g25_population_samples_G25PopulationSampleId",
                table: "research_links",
                column: "G25PopulationSampleId",
                principalTable: "g25_population_samples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

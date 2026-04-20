using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameG25PopulationSamplesToAdmixture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_population_samples_g25_admixture_eras_G25AdmixtureEraId",
                table: "g25_population_samples");

            migrationBuilder.DropForeignKey(
                name: "FK_research_links_g25_population_samples_G25AdmixturePopulatio~",
                table: "research_links");

            migrationBuilder.DropPrimaryKey(
                name: "PK_g25_population_samples",
                table: "g25_population_samples");

            migrationBuilder.RenameTable(
                name: "g25_population_samples",
                newName: "g25_admixture_population_samples");

            migrationBuilder.RenameIndex(
                name: "IX_g25_population_samples_Label",
                table: "g25_admixture_population_samples",
                newName: "IX_g25_admixture_population_samples_Label");

            migrationBuilder.RenameIndex(
                name: "IX_g25_population_samples_G25AdmixtureEraId",
                table: "g25_admixture_population_samples",
                newName: "IX_g25_admixture_population_samples_G25AdmixtureEraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_g25_admixture_population_samples",
                table: "g25_admixture_population_samples",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_admixture_population_samples_g25_admixture_eras_G25Admi~",
                table: "g25_admixture_population_samples",
                column: "G25AdmixtureEraId",
                principalTable: "g25_admixture_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_research_links_g25_admixture_population_samples_G25Admixtur~",
                table: "research_links",
                column: "G25AdmixturePopulationSampleId",
                principalTable: "g25_admixture_population_samples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_admixture_population_samples_g25_admixture_eras_G25Admi~",
                table: "g25_admixture_population_samples");

            migrationBuilder.DropForeignKey(
                name: "FK_research_links_g25_admixture_population_samples_G25Admixtur~",
                table: "research_links");

            migrationBuilder.DropPrimaryKey(
                name: "PK_g25_admixture_population_samples",
                table: "g25_admixture_population_samples");

            migrationBuilder.RenameTable(
                name: "g25_admixture_population_samples",
                newName: "g25_population_samples");

            migrationBuilder.RenameIndex(
                name: "IX_g25_admixture_population_samples_Label",
                table: "g25_population_samples",
                newName: "IX_g25_population_samples_Label");

            migrationBuilder.RenameIndex(
                name: "IX_g25_admixture_population_samples_G25AdmixtureEraId",
                table: "g25_population_samples",
                newName: "IX_g25_population_samples_G25AdmixtureEraId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_g25_population_samples",
                table: "g25_population_samples",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_population_samples_g25_admixture_eras_G25AdmixtureEraId",
                table: "g25_population_samples",
                column: "G25AdmixtureEraId",
                principalTable: "g25_admixture_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_research_links_g25_population_samples_G25AdmixturePopulatio~",
                table: "research_links",
                column: "G25AdmixturePopulationSampleId",
                principalTable: "g25_population_samples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

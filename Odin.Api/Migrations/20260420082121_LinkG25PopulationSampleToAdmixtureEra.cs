using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkG25PopulationSampleToAdmixtureEra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "G25AdmixtureEraId",
                table: "g25_population_samples",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_population_samples_G25AdmixtureEraId",
                table: "g25_population_samples",
                column: "G25AdmixtureEraId");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_population_samples_g25_admixture_eras_G25AdmixtureEraId",
                table: "g25_population_samples",
                column: "G25AdmixtureEraId",
                principalTable: "g25_admixture_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_population_samples_g25_admixture_eras_G25AdmixtureEraId",
                table: "g25_population_samples");

            migrationBuilder.DropIndex(
                name: "IX_g25_population_samples_G25AdmixtureEraId",
                table: "g25_population_samples");

            migrationBuilder.DropColumn(
                name: "G25AdmixtureEraId",
                table: "g25_population_samples");
        }
    }
}

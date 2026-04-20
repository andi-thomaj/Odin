using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddG25PcaPopulationsSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "G25PcaPopulationsSampleId",
                table: "research_links",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "g25_pca_populations_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    Ids = table.Column<string>(type: "text", nullable: false),
                    G25DistanceEraId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_pca_populations_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_pca_populations_samples_g25_distance_eras_G25DistanceEr~",
                        column: x => x.G25DistanceEraId,
                        principalTable: "g25_distance_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_research_links_G25PcaPopulationsSampleId",
                table: "research_links",
                column: "G25PcaPopulationsSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_populations_samples_G25DistanceEraId",
                table: "g25_pca_populations_samples",
                column: "G25DistanceEraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_populations_samples_Label",
                table: "g25_pca_populations_samples",
                column: "Label");

            migrationBuilder.AddForeignKey(
                name: "FK_research_links_g25_pca_populations_samples_G25PcaPopulation~",
                table: "research_links",
                column: "G25PcaPopulationsSampleId",
                principalTable: "g25_pca_populations_samples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_research_links_g25_pca_populations_samples_G25PcaPopulation~",
                table: "research_links");

            migrationBuilder.DropTable(
                name: "g25_pca_populations_samples");

            migrationBuilder.DropIndex(
                name: "IX_research_links_G25PcaPopulationsSampleId",
                table: "research_links");

            migrationBuilder.DropColumn(
                name: "G25PcaPopulationsSampleId",
                table: "research_links");
        }
    }
}

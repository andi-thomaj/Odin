using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RestoreG25ContinentPcaResultsVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "G25ContinentId",
                table: "g25_ethnicities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResultsVersion",
                table: "g25_distance_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResultsVersion",
                table: "g25_admixture_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "g25_continents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_continents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "g25_pca_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    G25EraId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_pca_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_pca_files_g25_eras_G25EraId",
                        column: x => x.G25EraId,
                        principalTable: "g25_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspection_continents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25ContinentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspection_continents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_continents_g25_continents_G25Contine~",
                        column: x => x.G25ContinentId,
                        principalTable: "g25_continents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_continents_g25_genetic_inspections_G~",
                        column: x => x.G25GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_pca_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25ContinentId = table.Column<int>(type: "integer", nullable: false),
                    ResultsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_pca_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_pca_results_g25_continents_G25ContinentId",
                        column: x => x.G25ContinentId,
                        principalTable: "g25_continents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_pca_results_g25_genetic_inspections_GeneticInspectionId",
                        column: x => x.GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_pca_result_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25PcaResultId = table.Column<int>(type: "integer", nullable: false),
                    G25PcaFileId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_pca_result_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_pca_result_files_g25_pca_files_G25PcaFileId",
                        column: x => x.G25PcaFileId,
                        principalTable: "g25_pca_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_pca_result_files_g25_pca_results_G25PcaResultId",
                        column: x => x.G25PcaResultId,
                        principalTable: "g25_pca_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_g25_ethnicities_G25ContinentId",
                table: "g25_ethnicities",
                column: "G25ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_continents_Name",
                table: "g25_continents",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_continents_G25ContinentId",
                table: "g25_genetic_inspection_continents",
                column: "G25ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_continents_G25GeneticInspectionId_G2~",
                table: "g25_genetic_inspection_continents",
                columns: new[] { "G25GeneticInspectionId", "G25ContinentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_files_G25EraId",
                table: "g25_pca_files",
                column: "G25EraId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_files_Title",
                table: "g25_pca_files",
                column: "Title",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_result_files_G25PcaFileId",
                table: "g25_pca_result_files",
                column: "G25PcaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_result_files_G25PcaResultId_G25PcaFileId",
                table: "g25_pca_result_files",
                columns: new[] { "G25PcaResultId", "G25PcaFileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_results_G25ContinentId",
                table: "g25_pca_results",
                column: "G25ContinentId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_results_GeneticInspectionId_G25ContinentId",
                table: "g25_pca_results",
                columns: new[] { "GeneticInspectionId", "G25ContinentId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_ethnicities_g25_continents_G25ContinentId",
                table: "g25_ethnicities",
                column: "G25ContinentId",
                principalTable: "g25_continents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_ethnicities_g25_continents_G25ContinentId",
                table: "g25_ethnicities");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspection_continents");

            migrationBuilder.DropTable(
                name: "g25_pca_result_files");

            migrationBuilder.DropTable(
                name: "g25_pca_files");

            migrationBuilder.DropTable(
                name: "g25_pca_results");

            migrationBuilder.DropTable(
                name: "g25_continents");

            migrationBuilder.DropIndex(
                name: "IX_g25_ethnicities_G25ContinentId",
                table: "g25_ethnicities");

            migrationBuilder.DropColumn(
                name: "G25ContinentId",
                table: "g25_ethnicities");

            migrationBuilder.DropColumn(
                name: "ResultsVersion",
                table: "g25_distance_results");

            migrationBuilder.DropColumn(
                name: "ResultsVersion",
                table: "g25_admixture_results");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddG25PerEraAdmixtureAndResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_files_G25EthnicityId",
                table: "g25_admixture_files");

            migrationBuilder.AddColumn<int>(
                name: "G25EraId",
                table: "g25_admixture_files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "g25_admixture_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25EraId = table.Column<int>(type: "integer", nullable: false),
                    FitDistance = table.Column<double>(type: "double precision", precision: 18, scale: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    Ancestors = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_admixture_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_admixture_results_g25_eras_G25EraId",
                        column: x => x.G25EraId,
                        principalTable: "g25_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_admixture_results_g25_genetic_inspections_GeneticInspec~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_distance_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25EraId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    Populations = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_distance_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_distance_results_g25_eras_G25EraId",
                        column: x => x.G25EraId,
                        principalTable: "g25_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_distance_results_g25_genetic_inspections_GeneticInspect~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspection_ethnicities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25EthnicityId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspection_ethnicities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_ethnicities_g25_ethnicities_G25Ethni~",
                        column: x => x.G25EthnicityId,
                        principalTable: "g25_ethnicities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_ethnicities_g25_genetic_inspections_~",
                        column: x => x.G25GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_files_G25EraId",
                table: "g25_admixture_files",
                column: "G25EraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_files_G25EthnicityId_G25EraId",
                table: "g25_admixture_files",
                columns: new[] { "G25EthnicityId", "G25EraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_results_G25EraId",
                table: "g25_admixture_results",
                column: "G25EraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_results_GeneticInspectionId_G25EraId",
                table: "g25_admixture_results",
                columns: new[] { "GeneticInspectionId", "G25EraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_results_G25EraId",
                table: "g25_distance_results",
                column: "G25EraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_results_GeneticInspectionId_G25EraId",
                table: "g25_distance_results",
                columns: new[] { "GeneticInspectionId", "G25EraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_ethnicities_G25EthnicityId",
                table: "g25_genetic_inspection_ethnicities",
                column: "G25EthnicityId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_ethnicities_G25GeneticInspectionId_G~",
                table: "g25_genetic_inspection_ethnicities",
                columns: new[] { "G25GeneticInspectionId", "G25EthnicityId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_admixture_files_g25_eras_G25EraId",
                table: "g25_admixture_files",
                column: "G25EraId",
                principalTable: "g25_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_admixture_files_g25_eras_G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.DropTable(
                name: "g25_admixture_results");

            migrationBuilder.DropTable(
                name: "g25_distance_results");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspection_ethnicities");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_files_G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_files_G25EthnicityId_G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.DropColumn(
                name: "G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_files_G25EthnicityId",
                table: "g25_admixture_files",
                column: "G25EthnicityId",
                unique: true);
        }
    }
}

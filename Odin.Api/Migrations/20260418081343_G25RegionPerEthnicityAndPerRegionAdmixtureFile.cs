using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class G25RegionPerEthnicityAndPerRegionAdmixtureFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_admixture_files_g25_eras_G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_admixture_files_g25_ethnicities_G25EthnicityId",
                table: "g25_admixture_files");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_admixture_results_g25_eras_G25EraId",
                table: "g25_admixture_results");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_ethnicities_g25_regions_G25RegionId",
                table: "g25_ethnicities");

            migrationBuilder.DropIndex(
                name: "IX_g25_regions_Name",
                table: "g25_regions");

            migrationBuilder.DropIndex(
                name: "IX_g25_ethnicities_G25RegionId",
                table: "g25_ethnicities");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_results_G25EraId",
                table: "g25_admixture_results");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_results_GeneticInspectionId_G25EraId",
                table: "g25_admixture_results");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_files_G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_files_G25EthnicityId_G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.DropColumn(
                name: "G25RegionId",
                table: "g25_ethnicities");

            migrationBuilder.DropColumn(
                name: "G25EraId",
                table: "g25_admixture_results");

            migrationBuilder.DropColumn(
                name: "G25EraId",
                table: "g25_admixture_files");

            migrationBuilder.RenameColumn(
                name: "G25EthnicityId",
                table: "g25_admixture_files",
                newName: "G25RegionId");

            migrationBuilder.AddColumn<int>(
                name: "G25EthnicityId",
                table: "g25_regions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "g25_genetic_inspection_regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    G25RegionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_genetic_inspection_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_regions_g25_genetic_inspections_G25G~",
                        column: x => x.G25GeneticInspectionId,
                        principalTable: "g25_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_g25_genetic_inspection_regions_g25_regions_G25RegionId",
                        column: x => x.G25RegionId,
                        principalTable: "g25_regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_g25_regions_G25EthnicityId_Name",
                table: "g25_regions",
                columns: new[] { "G25EthnicityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_results_GeneticInspectionId",
                table: "g25_admixture_results",
                column: "GeneticInspectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_files_G25RegionId",
                table: "g25_admixture_files",
                column: "G25RegionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_regions_G25GeneticInspectionId_G25Re~",
                table: "g25_genetic_inspection_regions",
                columns: new[] { "G25GeneticInspectionId", "G25RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_genetic_inspection_regions_G25RegionId",
                table: "g25_genetic_inspection_regions",
                column: "G25RegionId");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_admixture_files_g25_regions_G25RegionId",
                table: "g25_admixture_files",
                column: "G25RegionId",
                principalTable: "g25_regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_regions_g25_ethnicities_G25EthnicityId",
                table: "g25_regions",
                column: "G25EthnicityId",
                principalTable: "g25_ethnicities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_admixture_files_g25_regions_G25RegionId",
                table: "g25_admixture_files");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_regions_g25_ethnicities_G25EthnicityId",
                table: "g25_regions");

            migrationBuilder.DropTable(
                name: "g25_genetic_inspection_regions");

            migrationBuilder.DropIndex(
                name: "IX_g25_regions_G25EthnicityId_Name",
                table: "g25_regions");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_results_GeneticInspectionId",
                table: "g25_admixture_results");

            migrationBuilder.DropIndex(
                name: "IX_g25_admixture_files_G25RegionId",
                table: "g25_admixture_files");

            migrationBuilder.DropColumn(
                name: "G25EthnicityId",
                table: "g25_regions");

            migrationBuilder.RenameColumn(
                name: "G25RegionId",
                table: "g25_admixture_files",
                newName: "G25EthnicityId");

            migrationBuilder.AddColumn<int>(
                name: "G25RegionId",
                table: "g25_ethnicities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "G25EraId",
                table: "g25_admixture_results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "G25EraId",
                table: "g25_admixture_files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_g25_regions_Name",
                table: "g25_regions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_ethnicities_G25RegionId",
                table: "g25_ethnicities",
                column: "G25RegionId");

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
                name: "IX_g25_admixture_files_G25EraId",
                table: "g25_admixture_files",
                column: "G25EraId");

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_files_G25EthnicityId_G25EraId",
                table: "g25_admixture_files",
                columns: new[] { "G25EthnicityId", "G25EraId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_admixture_files_g25_eras_G25EraId",
                table: "g25_admixture_files",
                column: "G25EraId",
                principalTable: "g25_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_admixture_files_g25_ethnicities_G25EthnicityId",
                table: "g25_admixture_files",
                column: "G25EthnicityId",
                principalTable: "g25_ethnicities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_admixture_results_g25_eras_G25EraId",
                table: "g25_admixture_results",
                column: "G25EraId",
                principalTable: "g25_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_ethnicities_g25_regions_G25RegionId",
                table: "g25_ethnicities",
                column: "G25RegionId",
                principalTable: "g25_regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

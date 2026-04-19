using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameG25ErasToDistanceErasAndAddAdmixtureEras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_g25_distance_files_g25_eras_G25EraId",
                table: "g25_distance_files");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_distance_results_g25_eras_G25EraId",
                table: "g25_distance_results");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_pca_files_g25_eras_G25EraId",
                table: "g25_pca_files");

            migrationBuilder.RenameTable(
                name: "g25_eras",
                newName: "g25_distance_eras");

            migrationBuilder.RenameIndex(
                name: "IX_g25_eras_Name",
                table: "g25_distance_eras",
                newName: "IX_g25_distance_eras_Name");

            migrationBuilder.Sql(@"ALTER TABLE g25_distance_eras RENAME CONSTRAINT ""PK_g25_eras"" TO ""PK_g25_distance_eras"";");

            migrationBuilder.RenameColumn(
                name: "G25EraId",
                table: "g25_pca_files",
                newName: "G25DistanceEraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_pca_files_G25EraId",
                table: "g25_pca_files",
                newName: "IX_g25_pca_files_G25DistanceEraId");

            migrationBuilder.RenameColumn(
                name: "G25EraId",
                table: "g25_distance_results",
                newName: "G25DistanceEraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_distance_results_GeneticInspectionId_G25EraId",
                table: "g25_distance_results",
                newName: "IX_g25_distance_results_GeneticInspectionId_G25DistanceEraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_distance_results_G25EraId",
                table: "g25_distance_results",
                newName: "IX_g25_distance_results_G25DistanceEraId");

            migrationBuilder.RenameColumn(
                name: "G25EraId",
                table: "g25_distance_files",
                newName: "G25DistanceEraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_distance_files_G25EraId",
                table: "g25_distance_files",
                newName: "IX_g25_distance_files_G25DistanceEraId");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_distance_files_g25_distance_eras_G25DistanceEraId",
                table: "g25_distance_files",
                column: "G25DistanceEraId",
                principalTable: "g25_distance_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_distance_results_g25_distance_eras_G25DistanceEraId",
                table: "g25_distance_results",
                column: "G25DistanceEraId",
                principalTable: "g25_distance_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_pca_files_g25_distance_eras_G25DistanceEraId",
                table: "g25_pca_files",
                column: "G25DistanceEraId",
                principalTable: "g25_distance_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateTable(
                name: "g25_admixture_eras",
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
                    table.PrimaryKey("PK_g25_admixture_eras", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_g25_admixture_eras_Name",
                table: "g25_admixture_eras",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "g25_admixture_eras");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_distance_files_g25_distance_eras_G25DistanceEraId",
                table: "g25_distance_files");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_distance_results_g25_distance_eras_G25DistanceEraId",
                table: "g25_distance_results");

            migrationBuilder.DropForeignKey(
                name: "FK_g25_pca_files_g25_distance_eras_G25DistanceEraId",
                table: "g25_pca_files");

            migrationBuilder.RenameColumn(
                name: "G25DistanceEraId",
                table: "g25_pca_files",
                newName: "G25EraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_pca_files_G25DistanceEraId",
                table: "g25_pca_files",
                newName: "IX_g25_pca_files_G25EraId");

            migrationBuilder.RenameColumn(
                name: "G25DistanceEraId",
                table: "g25_distance_results",
                newName: "G25EraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_distance_results_GeneticInspectionId_G25DistanceEraId",
                table: "g25_distance_results",
                newName: "IX_g25_distance_results_GeneticInspectionId_G25EraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_distance_results_G25DistanceEraId",
                table: "g25_distance_results",
                newName: "IX_g25_distance_results_G25EraId");

            migrationBuilder.RenameColumn(
                name: "G25DistanceEraId",
                table: "g25_distance_files",
                newName: "G25EraId");

            migrationBuilder.RenameIndex(
                name: "IX_g25_distance_files_G25DistanceEraId",
                table: "g25_distance_files",
                newName: "IX_g25_distance_files_G25EraId");

            migrationBuilder.Sql(@"ALTER TABLE g25_distance_eras RENAME CONSTRAINT ""PK_g25_distance_eras"" TO ""PK_g25_eras"";");

            migrationBuilder.RenameIndex(
                name: "IX_g25_distance_eras_Name",
                table: "g25_distance_eras",
                newName: "IX_g25_eras_Name");

            migrationBuilder.RenameTable(
                name: "g25_distance_eras",
                newName: "g25_eras");

            migrationBuilder.AddForeignKey(
                name: "FK_g25_distance_files_g25_eras_G25EraId",
                table: "g25_distance_files",
                column: "G25EraId",
                principalTable: "g25_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_distance_results_g25_eras_G25EraId",
                table: "g25_distance_results",
                column: "G25EraId",
                principalTable: "g25_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_g25_pca_files_g25_eras_G25EraId",
                table: "g25_pca_files",
                column: "G25EraId",
                principalTable: "g25_eras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

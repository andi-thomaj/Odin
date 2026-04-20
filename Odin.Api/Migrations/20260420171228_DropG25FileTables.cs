using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropG25FileTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "g25_admixture_files");

            migrationBuilder.DropTable(
                name: "g25_distance_files");

            migrationBuilder.DropTable(
                name: "g25_pca_result_files");

            migrationBuilder.DropTable(
                name: "g25_pca_files");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "g25_admixture_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25RegionId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_admixture_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_admixture_files_g25_regions_G25RegionId",
                        column: x => x.G25RegionId,
                        principalTable: "g25_regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_distance_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25DistanceEraId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_distance_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_distance_files_g25_distance_eras_G25DistanceEraId",
                        column: x => x.G25DistanceEraId,
                        principalTable: "g25_distance_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_pca_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25DistanceEraId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_g25_pca_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_g25_pca_files_g25_distance_eras_G25DistanceEraId",
                        column: x => x.G25DistanceEraId,
                        principalTable: "g25_distance_eras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "g25_pca_result_files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    G25PcaFileId = table.Column<int>(type: "integer", nullable: false),
                    G25PcaResultId = table.Column<int>(type: "integer", nullable: false),
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
                name: "IX_g25_admixture_files_G25RegionId",
                table: "g25_admixture_files",
                column: "G25RegionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_files_G25DistanceEraId",
                table: "g25_distance_files",
                column: "G25DistanceEraId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_files_Title",
                table: "g25_distance_files",
                column: "Title",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_g25_pca_files_G25DistanceEraId",
                table: "g25_pca_files",
                column: "G25DistanceEraId",
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
        }
    }
}

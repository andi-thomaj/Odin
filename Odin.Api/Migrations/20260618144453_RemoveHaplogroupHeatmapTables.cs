using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHaplogroupHeatmapTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "haplogroup_import_runs");

            migrationBuilder.DropTable(
                name: "modern_haplogroup_frequencies");

            migrationBuilder.DropTable(
                name: "y_haplogroup_samples");

            migrationBuilder.DropTable(
                name: "y_haplogroup_tree_nodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "haplogroup_import_runs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DatasetVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnresolvedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_haplogroup_import_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "modern_haplogroup_frequencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CladeNodeId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HcKey = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    License = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Lon = table.Column<double>(type: "double precision", nullable: true),
                    Percentage = table.Column<double>(type: "double precision", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    StudyCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modern_haplogroup_frequencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "y_haplogroup_samples",
                columns: table => new
                {
                    GeneticId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Assessment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DatasetVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateMeanBp = table.Column<double>(type: "double precision", nullable: true),
                    DateSdBp = table.Column<double>(type: "double precision", nullable: true),
                    Era = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FullDate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GroupId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IndividualId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Layer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Locality = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "AADR"),
                    TreeNodeId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    YIsogg = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    YManual = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    YTerminal = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_y_haplogroup_samples", x => x.GeneticId);
                });

            migrationBuilder.CreateTable(
                name: "y_haplogroup_tree_nodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CentroidLat = table.Column<double>(type: "double precision", nullable: true),
                    CentroidLon = table.Column<double>(type: "double precision", nullable: true),
                    DatasetVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Formed = table.Column<double>(type: "double precision", nullable: true),
                    ParentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Snps = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    SubtreeSampleCount = table.Column<int>(type: "integer", nullable: false),
                    Tmrca = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_y_haplogroup_tree_nodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_haplogroup_import_runs_StartedAt",
                table: "haplogroup_import_runs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_modern_haplogroup_frequencies_CladeNodeId",
                table: "modern_haplogroup_frequencies",
                column: "CladeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_y_haplogroup_samples_Layer",
                table: "y_haplogroup_samples",
                column: "Layer");

            migrationBuilder.CreateIndex(
                name: "IX_y_haplogroup_samples_TreeNodeId",
                table: "y_haplogroup_samples",
                column: "TreeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_y_haplogroup_tree_nodes_ParentId",
                table: "y_haplogroup_tree_nodes",
                column: "ParentId");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHaplogroupHeatmapTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "haplogroup_import_runs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DatasetVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    UnresolvedCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TriggeredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_haplogroup_import_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "y_haplogroup_samples",
                columns: table => new
                {
                    GeneticId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IndividualId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TreeNodeId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    YTerminal = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    YIsogg = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    YManual = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    DateMeanBp = table.Column<double>(type: "double precision", nullable: true),
                    DateSdBp = table.Column<double>(type: "double precision", nullable: true),
                    FullDate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Era = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Layer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Country = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Locality = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GroupId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Assessment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DatasetVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                    ParentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Tmrca = table.Column<double>(type: "double precision", nullable: true),
                    Formed = table.Column<double>(type: "double precision", nullable: true),
                    Snps = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    CentroidLat = table.Column<double>(type: "double precision", nullable: true),
                    CentroidLon = table.Column<double>(type: "double precision", nullable: true),
                    SubtreeSampleCount = table.Column<int>(type: "integer", nullable: false),
                    DatasetVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "haplogroup_import_runs");

            migrationBuilder.DropTable(
                name: "y_haplogroup_samples");

            migrationBuilder.DropTable(
                name: "y_haplogroup_tree_nodes");
        }
    }
}

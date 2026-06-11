using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQpadmCladeResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "qpadm_clade_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResultsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Clade = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    NextPredictionClade = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NextPredictionScore = table.Column<double>(type: "double precision", nullable: true),
                    Lineage = table.Column<List<string>>(type: "text[]", nullable: false),
                    PositivesUsed = table.Column<int>(type: "integer", nullable: false),
                    NegativesUsed = table.Column<int>(type: "integer", nullable: false),
                    YReads = table.Column<int>(type: "integer", nullable: true),
                    SourceFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EffectiveBuild = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Warning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false),
                    Downstream = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_clade_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_qpadm_clade_results_qpadm_genetic_inspections_GeneticInspec~",
                        column: x => x.GeneticInspectionId,
                        principalTable: "qpadm_genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_clade_results_GeneticInspectionId",
                table: "qpadm_clade_results",
                column: "GeneticInspectionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qpadm_clade_results");
        }
    }
}

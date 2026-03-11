using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVahaduoTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vahaduo_result_populations");

            migrationBuilder.DropTable(
                name: "vahaduo_results");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vahaduo_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneticInspectionId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vahaduo_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vahaduo_results_genetic_inspections_GeneticInspectionId",
                        column: x => x.GeneticInspectionId,
                        principalTable: "genetic_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vahaduo_result_populations",
                columns: table => new
                {
                    VahaduoResultId = table.Column<int>(type: "integer", nullable: false),
                    PopulationId = table.Column<int>(type: "integer", nullable: false),
                    Distance = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vahaduo_result_populations", x => new { x.VahaduoResultId, x.PopulationId });
                    table.ForeignKey(
                        name: "FK_vahaduo_result_populations_populations_PopulationId",
                        column: x => x.PopulationId,
                        principalTable: "populations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vahaduo_result_populations_vahaduo_results_VahaduoResultId",
                        column: x => x.VahaduoResultId,
                        principalTable: "vahaduo_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vahaduo_result_populations_PopulationId",
                table: "vahaduo_result_populations",
                column: "PopulationId");

            migrationBuilder.CreateIndex(
                name: "IX_vahaduo_results_GeneticInspectionId",
                table: "vahaduo_results",
                column: "GeneticInspectionId",
                unique: true);
        }
    }
}

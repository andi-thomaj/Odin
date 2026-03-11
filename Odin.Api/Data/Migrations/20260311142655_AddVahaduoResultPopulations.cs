using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVahaduoResultPopulations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "vahaduo_results",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "vahaduo_results",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "vahaduo_results",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "vahaduo_results",
                type: "text",
                nullable: false,
                defaultValue: "");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vahaduo_result_populations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "vahaduo_results");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "vahaduo_results");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "vahaduo_results");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "vahaduo_results");
        }
    }
}

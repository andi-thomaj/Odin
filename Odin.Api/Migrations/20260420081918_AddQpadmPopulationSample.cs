using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQpadmPopulationSample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "G25PopulationSampleId",
                table: "research_links",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "QpadmPopulationSampleId",
                table: "research_links",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "qpadm_population_samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Coordinates = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qpadm_population_samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_research_links_QpadmPopulationSampleId",
                table: "research_links",
                column: "QpadmPopulationSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_qpadm_population_samples_Label",
                table: "qpadm_population_samples",
                column: "Label");

            migrationBuilder.AddForeignKey(
                name: "FK_research_links_qpadm_population_samples_QpadmPopulationSamp~",
                table: "research_links",
                column: "QpadmPopulationSampleId",
                principalTable: "qpadm_population_samples",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_research_links_qpadm_population_samples_QpadmPopulationSamp~",
                table: "research_links");

            migrationBuilder.DropTable(
                name: "qpadm_population_samples");

            migrationBuilder.DropIndex(
                name: "IX_research_links_QpadmPopulationSampleId",
                table: "research_links");

            migrationBuilder.DropColumn(
                name: "QpadmPopulationSampleId",
                table: "research_links");

            migrationBuilder.AlterColumn<int>(
                name: "G25PopulationSampleId",
                table: "research_links",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}

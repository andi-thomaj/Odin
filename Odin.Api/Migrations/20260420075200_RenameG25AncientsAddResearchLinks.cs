using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameG25AncientsAddResearchLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "g25_ancients",
                newName: "g25_population_samples");

            migrationBuilder.RenameIndex(
                name: "IX_g25_ancients_Label",
                table: "g25_population_samples",
                newName: "IX_g25_population_samples_Label");

            migrationBuilder.Sql(@"ALTER TABLE g25_population_samples RENAME CONSTRAINT ""PK_g25_ancients"" TO ""PK_g25_population_samples"";");

            migrationBuilder.CreateTable(
                name: "research_links",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Link = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    G25PopulationSampleId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_research_links_g25_population_samples_G25PopulationSampleId",
                        column: x => x.G25PopulationSampleId,
                        principalTable: "g25_population_samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_research_links_G25PopulationSampleId",
                table: "research_links",
                column: "G25PopulationSampleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "research_links");

            migrationBuilder.Sql(@"ALTER TABLE g25_population_samples RENAME CONSTRAINT ""PK_g25_population_samples"" TO ""PK_g25_ancients"";");

            migrationBuilder.RenameIndex(
                name: "IX_g25_population_samples_Label",
                table: "g25_population_samples",
                newName: "IX_g25_ancients_Label");

            migrationBuilder.RenameTable(
                name: "g25_population_samples",
                newName: "g25_ancients");
        }
    }
}

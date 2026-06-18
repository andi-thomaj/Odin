using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddModernHaplogroupFrequency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "modern_haplogroup_frequencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HcKey = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CladeNodeId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Percentage = table.Column<double>(type: "double precision", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    StudyCount = table.Column<int>(type: "integer", nullable: false),
                    License = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modern_haplogroup_frequencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_modern_haplogroup_frequencies_CladeNodeId",
                table: "modern_haplogroup_frequencies",
                column: "CladeNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "modern_haplogroup_frequencies");
        }
    }
}

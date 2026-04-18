using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeG25DistanceFilePerEraUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_g25_distance_files_G25EraId",
                table: "g25_distance_files");

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_files_G25EraId",
                table: "g25_distance_files",
                column: "G25EraId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_g25_distance_files_G25EraId",
                table: "g25_distance_files");

            migrationBuilder.CreateIndex(
                name: "IX_g25_distance_files_G25EraId",
                table: "g25_distance_files",
                column: "G25EraId");
        }
    }
}

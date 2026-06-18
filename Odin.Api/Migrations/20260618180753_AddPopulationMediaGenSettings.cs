using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPopulationMediaGenSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePrompt",
                table: "qpadm_populations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VideoDurationSeconds",
                table: "qpadm_populations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoMode",
                table: "qpadm_populations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoModel",
                table: "qpadm_populations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoPrompt",
                table: "qpadm_populations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePrompt",
                table: "qpadm_populations");

            migrationBuilder.DropColumn(
                name: "VideoDurationSeconds",
                table: "qpadm_populations");

            migrationBuilder.DropColumn(
                name: "VideoMode",
                table: "qpadm_populations");

            migrationBuilder.DropColumn(
                name: "VideoModel",
                table: "qpadm_populations");

            migrationBuilder.DropColumn(
                name: "VideoPrompt",
                table: "qpadm_populations");
        }
    }
}

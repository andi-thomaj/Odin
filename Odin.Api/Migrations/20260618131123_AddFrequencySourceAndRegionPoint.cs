using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequencySourceAndRegionPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Lat",
                table: "modern_haplogroup_frequencies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Lon",
                table: "modern_haplogroup_frequencies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "modern_haplogroup_frequencies",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Lat",
                table: "modern_haplogroup_frequencies");

            migrationBuilder.DropColumn(
                name: "Lon",
                table: "modern_haplogroup_frequencies");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "modern_haplogroup_frequencies");
        }
    }
}

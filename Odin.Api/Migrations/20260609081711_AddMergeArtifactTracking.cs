using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeArtifactTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Converted23AndMeData",
                table: "raw_genetic_files",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Converted23AndMeFileName",
                table: "raw_genetic_files",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergeError",
                table: "raw_genetic_files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergeFileName",
                table: "raw_genetic_files",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergeId",
                table: "raw_genetic_files",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MergeSizeBytes",
                table: "raw_genetic_files",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergeStatus",
                table: "raw_genetic_files",
                type: "text",
                nullable: false,
                defaultValue: "NotStarted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Converted23AndMeData",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "Converted23AndMeFileName",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "MergeError",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "MergeFileName",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "MergeId",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "MergeSizeBytes",
                table: "raw_genetic_files");

            migrationBuilder.DropColumn(
                name: "MergeStatus",
                table: "raw_genetic_files");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKindToAdmixtureSavedFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_admixture_saved_files_UserId_UpdatedAt",
                table: "admixture_saved_files");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "admixture_saved_files",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "source");

            migrationBuilder.CreateIndex(
                name: "IX_admixture_saved_files_UserId_Kind_UpdatedAt",
                table: "admixture_saved_files",
                columns: new[] { "UserId", "Kind", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_admixture_saved_files_UserId_Kind_UpdatedAt",
                table: "admixture_saved_files");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "admixture_saved_files");

            migrationBuilder.CreateIndex(
                name: "IX_admixture_saved_files_UserId_UpdatedAt",
                table: "admixture_saved_files",
                columns: new[] { "UserId", "UpdatedAt" });
        }
    }
}

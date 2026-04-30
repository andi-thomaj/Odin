using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGifAvatarImageFromQpadmPopulations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GifAvatarImage",
                table: "qpadm_populations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "GifAvatarImage",
                table: "qpadm_populations",
                type: "bytea",
                nullable: true);
        }
    }
}

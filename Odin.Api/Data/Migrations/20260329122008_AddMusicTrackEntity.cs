using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicTrackEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MusicTrackId",
                table: "populations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "music_tracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_music_tracks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_populations_MusicTrackId",
                table: "populations",
                column: "MusicTrackId");

            migrationBuilder.AddForeignKey(
                name: "FK_populations_music_tracks_MusicTrackId",
                table: "populations",
                column: "MusicTrackId",
                principalTable: "music_tracks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_populations_music_tracks_MusicTrackId",
                table: "populations");

            migrationBuilder.DropTable(
                name: "music_tracks");

            migrationBuilder.DropIndex(
                name: "IX_populations_MusicTrackId",
                table: "populations");

            migrationBuilder.DropColumn(
                name: "MusicTrackId",
                table: "populations");
        }
    }
}

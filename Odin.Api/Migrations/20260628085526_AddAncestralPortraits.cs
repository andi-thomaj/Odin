using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAncestralPortraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ancestral_portrait_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ancestral_portrait_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ancestral_portrait_sets_application_users_UserId",
                        column: x => x.UserId,
                        principalTable: "application_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ancestral_portraits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EraId = table.Column<int>(type: "integer", nullable: false),
                    EraName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PopulationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    R2Key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    VariationIndex = table.Column<int>(type: "integer", nullable: false),
                    IsSelected = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ancestral_portraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ancestral_portraits_ancestral_portrait_sets_SetId",
                        column: x => x.SetId,
                        principalTable: "ancestral_portrait_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portrait_sets_OrderId",
                table: "ancestral_portrait_sets",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portrait_sets_TransactionId",
                table: "ancestral_portrait_sets",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portrait_sets_UserId",
                table: "ancestral_portrait_sets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portraits_SetId",
                table: "ancestral_portraits",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_ancestral_portraits_SetId_EraId",
                table: "ancestral_portraits",
                columns: new[] { "SetId", "EraId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ancestral_portraits");

            migrationBuilder.DropTable(
                name: "ancestral_portrait_sets");
        }
    }
}

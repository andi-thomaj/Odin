using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Odin.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImageGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "image_generation_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsAsync = table.Column<bool>(type: "boolean", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    RevisedPrompt = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Background = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OutputFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OutputCompression = table.Column<int>(type: "integer", nullable: true),
                    Moderation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    N = table.Column<int>(type: "integer", nullable: false),
                    ReferenceImageIds = table.Column<int[]>(type: "integer[]", nullable: true),
                    MaskReferenceImageId = table.Column<int>(type: "integer", nullable: true),
                    InputFidelity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UsageInputTokens = table.Column<long>(type: "bigint", nullable: true),
                    UsageOutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    UsageTotalTokens = table.Column<long>(type: "bigint", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_generation_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "image_generation_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Background = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OutputFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OutputCompression = table.Column<int>(type: "integer", nullable: true),
                    Moderation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultN = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_image_generation_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reference_images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    R2Key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reference_images", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "generated_images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchIndex = table.Column<int>(type: "integer", nullable: false),
                    R2Key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_generated_images_image_generation_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "image_generation_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generated_images_CreatedAt",
                table: "generated_images",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_generated_images_JobId",
                table: "generated_images",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_image_generation_jobs_CreatedAt",
                table: "image_generation_jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_image_generation_jobs_Status",
                table: "image_generation_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_reference_images_CreatedAt",
                table: "reference_images",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_images");

            migrationBuilder.DropTable(
                name: "image_generation_settings");

            migrationBuilder.DropTable(
                name: "reference_images");

            migrationBuilder.DropTable(
                name: "image_generation_jobs");
        }
    }
}

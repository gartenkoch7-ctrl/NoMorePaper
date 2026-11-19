using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMorePaper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecognitionStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_next_attempt_at",
                table: "processing_jobs");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "processing_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "stage",
                table: "processing_jobs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Ocr");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_priority_next_attempt_at",
                table: "processing_jobs",
                columns: new[] { "priority", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_processing_jobs_priority_next_attempt_at",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "processing_jobs");

            migrationBuilder.DropColumn(
                name: "stage",
                table: "processing_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_next_attempt_at",
                table: "processing_jobs",
                column: "next_attempt_at");
        }
    }
}

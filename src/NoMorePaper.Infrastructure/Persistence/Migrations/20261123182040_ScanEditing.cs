using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMorePaper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScanEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "split_suggestion",
                table: "documents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "version_policy",
                table: "documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "after_editing",
                table: "account_preferences",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "KeepBoth");

            migrationBuilder.CreateTable(
                name: "document_edits",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recognize_after = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_edits", x => x.document_id);
                    table.ForeignKey(
                        name: "FK_document_edits_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_pages",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    head = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    probably_blank = table.Column<bool>(type: "boolean", nullable: false),
                    had_text_layer = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_pages", x => new { x.document_id, x.page });
                    table.ForeignKey(
                        name: "FK_document_pages_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trash_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file = table.Column<string>(type: "jsonb", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purge_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trash_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_trash_items_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_deleted_at",
                table: "documents",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_trash_items_document_id",
                table: "trash_items",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_trash_items_purge_after",
                table: "trash_items",
                column: "purge_after");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_edits");

            migrationBuilder.DropTable(
                name: "document_pages");

            migrationBuilder.DropTable(
                name: "trash_items");

            migrationBuilder.DropIndex(
                name: "IX_documents_deleted_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "edited_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "split_suggestion",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "version_policy",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "after_editing",
                table: "account_preferences");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMorePaper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Classification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "documents",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correspondent_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "documents",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "document_date",
                table: "documents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "document_type_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "possible_duplicate_of",
                table: "documents",
                type: "uuid",
                nullable: true);

            // Edited by hand: existing documents are not recognized yet. The generated "" is no
            // RecognitionState and would make them unreadable.
            migrationBuilder.AddColumn<string>(
                name: "recognition",
                table: "documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "correspondents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correspondents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_editors",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_editors", x => new { x.document_id, x.account_id });
                    table.ForeignKey(
                        name: "FK_document_editors_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_editors_users_account_id",
                        column: x => x.account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_tags",
                columns: table => new
                {
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_tags", x => new { x.document_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_document_tags_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_correspondent_id",
                table: "documents",
                column: "correspondent_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_document_date",
                table: "documents",
                column: "document_date");

            migrationBuilder.CreateIndex(
                name: "IX_documents_document_type_id",
                table: "documents",
                column: "document_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_possible_duplicate_of",
                table: "documents",
                column: "possible_duplicate_of");

            migrationBuilder.CreateIndex(
                name: "IX_document_files_sha256",
                table: "document_files",
                column: "sha256",
                filter: "kind = 'Original'");

            migrationBuilder.CreateIndex(
                name: "IX_correspondents_normalized_name",
                table: "correspondents",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "IX_document_editors_account_id",
                table: "document_editors",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_tags_tag_id",
                table: "document_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_types_normalized_name",
                table: "document_types",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "IX_tags_normalized_name",
                table: "tags",
                column: "normalized_name");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_correspondents_correspondent_id",
                table: "documents",
                column: "correspondent_id",
                principalTable: "correspondents",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_document_types_document_type_id",
                table: "documents",
                column: "document_type_id",
                principalTable: "document_types",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_documents_possible_duplicate_of",
                table: "documents",
                column: "possible_duplicate_of",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_correspondents_correspondent_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_document_types_document_type_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_documents_possible_duplicate_of",
                table: "documents");

            migrationBuilder.DropTable(
                name: "correspondents");

            migrationBuilder.DropTable(
                name: "document_editors");

            migrationBuilder.DropTable(
                name: "document_tags");

            migrationBuilder.DropTable(
                name: "document_types");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropIndex(
                name: "IX_documents_correspondent_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_document_date",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_document_type_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_possible_duplicate_of",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_document_files_sha256",
                table: "document_files");

            migrationBuilder.DropColumn(
                name: "amount",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "correspondent_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "document_date",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "document_type_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "possible_duplicate_of",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "recognition",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "documents");
        }
    }
}

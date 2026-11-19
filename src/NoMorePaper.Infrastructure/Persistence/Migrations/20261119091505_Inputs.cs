using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMorePaper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mail_note",
                table: "documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mail_sender",
                table: "documents",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mail_subject",
                table: "documents",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "needs_owner_check",
                table: "documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Upload");

            migrationBuilder.CreateTable(
                name: "account_mail_senders",
                columns: table => new
                {
                    address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_mail_senders", x => x.address);
                    table.ForeignKey(
                        name: "FK_account_mail_senders_users_account_id",
                        column: x => x.account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_preferences",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_documents_private = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_preferences", x => x.account_id);
                    table.ForeignKey(
                        name: "FK_account_preferences_users_account_id",
                        column: x => x.account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "input_connections",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_key = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settings = table.Column<string>(type: "jsonb", nullable: false),
                    secret = table.Column<byte[]>(type: "bytea", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_input_connections", x => new { x.kind, x.account_key });
                    table.ForeignKey(
                        name: "FK_input_connections_users_account_id",
                        column: x => x.account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "input_seen_items",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_key = table.Column<Guid>(type: "uuid", nullable: false),
                    list = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    key_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    failures = table.Column<int>(type: "integer", nullable: false),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_input_seen_items", x => new { x.kind, x.account_key, x.list, x.key_hash });
                    table.ForeignKey(
                        name: "FK_input_seen_items_users_account_id",
                        column: x => x.account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "input_status",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_key = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    problem = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    needs_attention = table.Column<bool>(type: "boolean", nullable: false),
                    received_total = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_input_status", x => new { x.kind, x.account_key });
                    table.ForeignKey(
                        name: "FK_input_status_users_account_id",
                        column: x => x.account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_mail_senders_account_id",
                table: "account_mail_senders",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_input_connections_account_id",
                table: "input_connections",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_input_seen_items_account_id",
                table: "input_seen_items",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_input_status_account_id",
                table: "input_status",
                column: "account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_mail_senders");

            migrationBuilder.DropTable(
                name: "account_preferences");

            migrationBuilder.DropTable(
                name: "input_connections");

            migrationBuilder.DropTable(
                name: "input_seen_items");

            migrationBuilder.DropTable(
                name: "input_status");

            migrationBuilder.DropColumn(
                name: "mail_note",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "mail_sender",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "mail_subject",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "needs_owner_check",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "source",
                table: "documents");
        }
    }
}

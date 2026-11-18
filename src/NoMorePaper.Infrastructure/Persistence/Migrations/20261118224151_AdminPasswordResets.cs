using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMorePaper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminPasswordResets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_password_resets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reset_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_password_resets", x => x.id);
                    table.ForeignKey(
                        name: "FK_admin_password_resets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_password_resets_user_id",
                table: "admin_password_resets",
                column: "user_id");

            // Hand-written: an unacknowledged notice from the single column must survive the move.
            migrationBuilder.Sql(
                """
                INSERT INTO admin_password_resets (id, user_id, reset_at, acknowledged_at)
                SELECT gen_random_uuid(), id, password_reset_by_admin_at, NULL
                FROM users
                WHERE password_reset_by_admin_at IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "password_reset_by_admin_at",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_reset_by_admin_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Hand-written: the single column can only keep the newest unacknowledged reset.
            migrationBuilder.Sql(
                """
                UPDATE users SET password_reset_by_admin_at = latest.reset_at
                FROM (
                    SELECT user_id, max(reset_at) AS reset_at
                    FROM admin_password_resets
                    WHERE acknowledged_at IS NULL
                    GROUP BY user_id
                ) AS latest
                WHERE users.id = latest.user_id;
                """);

            migrationBuilder.DropTable(
                name: "admin_password_resets");
        }
    }
}

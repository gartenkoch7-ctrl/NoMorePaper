using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoMorePaper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminResetNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_reset_by_admin_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_reset_by_admin_at",
                table: "users");
        }
    }
}

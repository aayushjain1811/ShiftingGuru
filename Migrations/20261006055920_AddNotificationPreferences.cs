using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    IdentityUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Leads = table.Column<bool>(type: "boolean", nullable: false),
                    Quotes = table.Column<bool>(type: "boolean", nullable: false),
                    Bookings = table.Column<bool>(type: "boolean", nullable: false),
                    Reviews = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.IdentityUserId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationPreferences");
        }
    }
}

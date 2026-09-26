using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class PartnerFreeTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationFeePaidAt",
                table: "Vendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Vendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialStartedAt",
                table: "Vendors",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationFeePaidAt",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "TrialStartedAt",
                table: "Vendors");
        }
    }
}

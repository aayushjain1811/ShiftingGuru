using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RazorpayOrderId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RazorpayPaymentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VendorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationPayments_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPayments_Email_CreatedAt",
                table: "RegistrationPayments",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPayments_RazorpayOrderId",
                table: "RegistrationPayments",
                column: "RazorpayOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPayments_Status",
                table: "RegistrationPayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPayments_VendorId",
                table: "RegistrationPayments",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrationPayments");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "vendor_number_seq");

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VendorNumber = table.Column<string>(type: "character varying(28)", maxLength: 28, nullable: false),
                    IdentityUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    GstNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: false),
                    OperatingLocations = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    AdditionalInformation = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    ServiceSlug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorServices_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_City",
                table: "Vendors",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_CreatedAt",
                table: "Vendors",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Email",
                table: "Vendors",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_IdentityUserId",
                table: "Vendors",
                column: "IdentityUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Phone",
                table: "Vendors",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Status",
                table: "Vendors",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_VendorNumber",
                table: "Vendors",
                column: "VendorNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorServices_ServiceSlug",
                table: "VendorServices",
                column: "ServiceSlug");

            migrationBuilder.CreateIndex(
                name: "IX_VendorServices_VendorId_ServiceSlug",
                table: "VendorServices",
                columns: new[] { "VendorId", "ServiceSlug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorServices");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropSequence(
                name: "vendor_number_seq");
        }
    }
}

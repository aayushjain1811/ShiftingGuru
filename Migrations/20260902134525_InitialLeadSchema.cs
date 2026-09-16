using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class InitialLeadSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "lead_number_seq");

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadNumber = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ServiceSlug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MovingFrom = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MovingTo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    StorageLocation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MovingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PropertyType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MoveSize = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OfficeSize = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DeskCount = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    VehicleType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    VehicleModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    VehicleCondition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GoodsType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LoadDetails = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    VehicleRequirement = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    StorageType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    StorageSize = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    StorageDuration = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PreferredContactMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AdditionalRequirements = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CreatedAt",
                table: "Leads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadNumber",
                table: "Leads",
                column: "LeadNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Phone_CreatedAt",
                table: "Leads",
                columns: new[] { "Phone", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status",
                table: "Leads",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropSequence(
                name: "lead_number_seq");
        }
    }
}

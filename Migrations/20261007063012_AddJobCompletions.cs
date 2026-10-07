using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobCompletions",
                columns: table => new
                {
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<int>(type: "integer", nullable: false),
                    PartnerMarkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PartnerNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomerAnswer = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerAnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProblemText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobCompletions", x => x.LeadId);
                    table.ForeignKey(
                        name: "FK_JobCompletions_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobCompletions_CustomerAnswer",
                table: "JobCompletions",
                column: "CustomerAnswer");

            migrationBuilder.CreateIndex(
                name: "IX_JobCompletions_PartnerMarkedAt",
                table: "JobCompletions",
                column: "PartnerMarkedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobCompletions_VendorId",
                table: "JobCompletions",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobCompletions");
        }
    }
}

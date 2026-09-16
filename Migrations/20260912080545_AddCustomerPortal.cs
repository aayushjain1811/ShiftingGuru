using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedAt",
                table: "Leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelectedQuoteId",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelectedVendorId",
                table: "Leads",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerAccessTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAccessTokens_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ConvertedAt",
                table: "Leads",
                column: "ConvertedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_SelectedQuoteId",
                table: "Leads",
                column: "SelectedQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_SelectedVendorId",
                table: "Leads",
                column: "SelectedVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccessTokens_ExpiresAt",
                table: "CustomerAccessTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccessTokens_LeadId",
                table: "CustomerAccessTokens",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccessTokens_TokenHash",
                table: "CustomerAccessTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Quotes_SelectedQuoteId",
                table: "Leads",
                column: "SelectedQuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Vendors_SelectedVendorId",
                table: "Leads",
                column: "SelectedVendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Quotes_SelectedQuoteId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Vendors_SelectedVendorId",
                table: "Leads");

            migrationBuilder.DropTable(
                name: "CustomerAccessTokens");

            migrationBuilder.DropIndex(
                name: "IX_Leads_ConvertedAt",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_SelectedQuoteId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_SelectedVendorId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ConvertedAt",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "SelectedQuoteId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "SelectedVendorId",
                table: "Leads");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShiftingGuru.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Country = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    H1 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MetaTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    OgTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    OgDescription = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    OgImage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromLocationId = table.Column<int>(type: "integer", nullable: false),
                    ToLocationId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    H1 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    MetaTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MetaDescription = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    OgTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    OgDescription = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    OgImage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => x.Id);
                    table.CheckConstraint("ck_routes_distinct_endpoints", "\"FromLocationId\" <> \"ToLocationId\"");
                    table.ForeignKey(
                        name: "FK_Routes_Locations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Routes_Locations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Faqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    RouteId = table.Column<int>(type: "integer", nullable: true),
                    ServiceSlug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faqs", x => x.Id);
                    table.CheckConstraint("ck_faqs_single_owner", "(CASE WHEN \"LocationId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"RouteId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ServiceSlug\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_Faqs_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Faqs_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Faqs_LocationId_DisplayOrder",
                table: "Faqs",
                columns: new[] { "LocationId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Faqs_RouteId_DisplayOrder",
                table: "Faqs",
                columns: new[] { "RouteId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Faqs_ServiceSlug_DisplayOrder",
                table: "Faqs",
                columns: new[] { "ServiceSlug", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_IsPublished",
                table: "Locations",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Slug",
                table: "Locations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_FromLocationId_ToLocationId",
                table: "Routes",
                columns: new[] { "FromLocationId", "ToLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_IsPublished",
                table: "Routes",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Slug",
                table: "Routes",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_ToLocationId",
                table: "Routes",
                column: "ToLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Faqs");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "Locations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Marketplace.Migrations
{
    /// <inheritdoc />
    public partial class AddChatReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReporterId = table.Column<string>(type: "text", nullable: false),
                    ReportedUserId = table.Column<string>(type: "text", nullable: false),
                    AdvertisementId = table.Column<int>(type: "integer", nullable: false),
                    ThreadKey = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedByAdminId = table.Column<string>(type: "text", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActionTaken = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatReports_Advertisements_AdvertisementId",
                        column: x => x.AdvertisementId,
                        principalTable: "Advertisements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatReports_AspNetUsers_ReportedUserId",
                        column: x => x.ReportedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatReports_AspNetUsers_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockerId",
                table: "UserBlocks",
                column: "BlockerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatReports_AdvertisementId",
                table: "ChatReports",
                column: "AdvertisementId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatReports_CreatedAtUtc",
                table: "ChatReports",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ChatReports_ReportedUserId_Status",
                table: "ChatReports",
                columns: new[] { "ReportedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatReports_ReporterId_ThreadKey",
                table: "ChatReports",
                columns: new[] { "ReporterId", "ThreadKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatReports_ReviewedByAdminId",
                table: "ChatReports",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatReports_Status",
                table: "ChatReports",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatReports");

            migrationBuilder.DropIndex(
                name: "IX_UserBlocks_BlockerId",
                table: "UserBlocks");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Migrations
{
    /// <inheritdoc />
    public partial class FixChatReportReviewedByAdminSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatReports_AspNetUsers_ReviewedByAdminId",
                table: "ChatReports");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatReports_AspNetUsers_ReviewedByAdminId",
                table: "ChatReports",
                column: "ReviewedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatReports_AspNetUsers_ReviewedByAdminId",
                table: "ChatReports");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatReports_AspNetUsers_ReviewedByAdminId",
                table: "ChatReports",
                column: "ReviewedByAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}

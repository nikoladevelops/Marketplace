using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Migrations
{
    public partial class RenamedColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Image",
                table: "Advertisements",
                newName: "ImageData");

            migrationBuilder.RenameColumn(
                name: "Image",
                table: "AdvertisementImages",
                newName: "ImageData");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageData",
                table: "Advertisements",
                newName: "Image");

            migrationBuilder.RenameColumn(
                name: "ImageData",
                table: "AdvertisementImages",
                newName: "Image");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Migrations
{
    public partial class SeedUserRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT INTO AspNetRoles (Id,Name) VALUES(1,'Seller')");
            migrationBuilder.Sql("INSERT INTO AspNetRoles (Id,Name) VALUES(2,'Admin')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

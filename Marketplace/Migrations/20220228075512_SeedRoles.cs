using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Migrations
{
    public partial class SeedRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM AspNetRoles");
            migrationBuilder.Sql("INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'53d6daf9-1c7e-47b5-9304-1318eadca574', N'Admin', N'ADMIN', N'79d01f45-2036-4662-832a-0e4db37851be')");
            migrationBuilder.Sql("INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'72d2ec76-57db-4fcf-be6d-83809ff76258', N'Seller', N'SELLER', N'a0025c3b-8655-4bde-8349-64003ebb1041')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Migrations
{
    public partial class SeedCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Phones')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Tablets')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Computers')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('TVs')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Home Appliances')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Sport')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Fashion')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Properties')");
            migrationBuilder.Sql("INSERT INTO Categories (Name) VALUES ('Other')");

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

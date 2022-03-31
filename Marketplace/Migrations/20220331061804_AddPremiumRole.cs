using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Migrations
{
    public partial class AddPremiumRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'b5457e0c-138e-43e4-b87b-8e3edcfcea7d', N'Premium', N'PREMIUM', N'20438ed1-7165-427a-8c07-2f8ee247939f')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

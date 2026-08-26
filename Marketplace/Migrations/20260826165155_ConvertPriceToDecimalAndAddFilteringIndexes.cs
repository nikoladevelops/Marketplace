using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPriceToDecimalAndAddFilteringIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Advertisements",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_CategoryId_Price",
                table: "Advertisements",
                columns: new[] { "CategoryId", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_DateCreatedOn",
                table: "Advertisements",
                column: "DateCreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_Description",
                table: "Advertisements",
                column: "Description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_Location",
                table: "Advertisements",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_Price",
                table: "Advertisements",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_Title",
                table: "Advertisements",
                column: "Title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Advertisements_CategoryId_Price",
                table: "Advertisements");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_DateCreatedOn",
                table: "Advertisements");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_Description",
                table: "Advertisements");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_Location",
                table: "Advertisements");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_Price",
                table: "Advertisements");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_Title",
                table: "Advertisements");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<double>(
                name: "Price",
                table: "Advertisements",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }
    }
}

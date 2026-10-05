using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceIdentityToBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "BrandingSettings",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AuthorizedDistributorOf",
                table: "BrandingSettings",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "BrandingSettings",
                type: "varchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "BrandingSettings",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "BrandingSettings",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "BrandingSettings");

            migrationBuilder.DropColumn(
                name: "AuthorizedDistributorOf",
                table: "BrandingSettings");

            migrationBuilder.DropColumn(
                name: "City",
                table: "BrandingSettings");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "BrandingSettings");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "BrandingSettings");
        }
    }
}

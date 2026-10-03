using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductMrp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Mrp",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Mrp",
                table: "OrderProducts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql("UPDATE `Products` SET `Mrp` = `SellingPrice` WHERE `Mrp` IS NULL");
            migrationBuilder.Sql("UPDATE `OrderProducts` AS op JOIN `Products` AS p ON op.`ProductId` = p.`Id` SET op.`Mrp` = p.`Mrp` WHERE op.`Mrp` IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mrp",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Mrp",
                table: "OrderProducts");
        }
    }
}

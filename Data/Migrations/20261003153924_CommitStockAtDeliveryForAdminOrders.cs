using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommitStockAtDeliveryForAdminOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "StockCommittedQuantity",
                table: "OrderProducts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
            // Older orders already reduced stock when they were placed.
            migrationBuilder.Sql("UPDATE `OrderProducts` SET `StockCommittedQuantity` = `Quantity` + `FreeIssueQuantity`");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockCommittedQuantity",
                table: "OrderProducts");
        }
    }
}

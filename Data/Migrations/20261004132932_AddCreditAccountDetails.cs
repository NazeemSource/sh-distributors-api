using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditAccountDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "DepositAccounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCreditAccount",
                table: "DepositAccounts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ShopId",
                table: "DepositAccounts",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepositAccounts_ShopId",
                table: "DepositAccounts",
                column: "ShopId");

            migrationBuilder.AddForeignKey(
                name: "FK_DepositAccounts_Shops_ShopId",
                table: "DepositAccounts",
                column: "ShopId",
                principalTable: "Shops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DepositAccounts_Shops_ShopId",
                table: "DepositAccounts");

            migrationBuilder.DropIndex(
                name: "IX_DepositAccounts_ShopId",
                table: "DepositAccounts");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "DepositAccounts");

            migrationBuilder.DropColumn(
                name: "IsCreditAccount",
                table: "DepositAccounts");

            migrationBuilder.DropColumn(
                name: "ShopId",
                table: "DepositAccounts");
        }
    }
}

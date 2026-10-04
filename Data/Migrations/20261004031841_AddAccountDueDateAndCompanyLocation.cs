using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDueDateAndCompanyLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "DepositAccounts",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "DepositAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Companies",
                type: "varchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Companies",
                type: "varchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE `Companies` SET `Category` = `Address` WHERE `Category` = '' AND `Address` <> ''");
            migrationBuilder.Sql("UPDATE `DepositAccounts` SET `DueDate` = `Date` WHERE `DueDate` IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "DepositAccounts");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Companies");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "DepositAccounts",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80);
        }
    }
}

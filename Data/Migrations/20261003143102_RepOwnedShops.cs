using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepOwnedShops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByRepId",
                table: "Shops",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shops_CreatedByRepId",
                table: "Shops",
                column: "CreatedByRepId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shops_Users_CreatedByRepId",
                table: "Shops",
                column: "CreatedByRepId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shops_Users_CreatedByRepId",
                table: "Shops");

            migrationBuilder.DropIndex(
                name: "IX_Shops_CreatedByRepId",
                table: "Shops");

            migrationBuilder.DropColumn(
                name: "CreatedByRepId",
                table: "Shops");
        }
    }
}

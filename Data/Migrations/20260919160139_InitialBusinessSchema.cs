using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBusinessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER DATABASE CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
            migrationBuilder.Sql("SET default_storage_engine = InnoDB;");

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false),
                    ContactName = table.Column<string>(type: "longtext", nullable: false),
                    Phone = table.Column<string>(type: "longtext", nullable: false),
                    Address = table.Column<string>(type: "longtext", nullable: false),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Sku = table.Column<string>(type: "varchar(255)", nullable: false),
                    Barcode = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false),
                    Category = table.Column<string>(type: "longtext", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Shops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Code = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false),
                    ContactName = table.Column<string>(type: "longtext", nullable: false),
                    Phone = table.Column<string>(type: "longtext", nullable: false),
                    Address = table.Column<string>(type: "longtext", nullable: false),
                    City = table.Column<string>(type: "longtext", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shops_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "StockIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: false),
                    StockInNumber = table.Column<string>(type: "varchar(255)", nullable: false),
                    StockInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StockTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "varchar(255)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockIns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Username = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    PasswordHash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Territory = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    Type = table.Column<string>(type: "longtext", nullable: false),
                    QuantityIn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityOut = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferenceType = table.Column<string>(type: "longtext", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Notes = table.Column<string>(type: "longtext", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "StockInPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    StockInId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Method = table.Column<string>(type: "longtext", nullable: false),
                    Reference = table.Column<string>(type: "longtext", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockInPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockInPayments_StockIns_StockInId",
                        column: x => x.StockInId,
                        principalTable: "StockIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "StockInProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    StockInId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockInProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockInProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockInProducts_StockIns_StockInId",
                        column: x => x.StockInId,
                        principalTable: "StockIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ShopId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SalesRepId = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrderNumber = table.Column<string>(type: "varchar(255)", nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "varchar(255)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "longtext", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Users_SalesRepId",
                        column: x => x.SalesRepId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "Cheques",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ShopId = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ChequeNumber = table.Column<string>(type: "varchar(255)", nullable: false),
                    BankName = table.Column<string>(type: "longtext", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChequeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(255)", nullable: false),
                    RemindBeforeDays = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cheques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cheques_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cheques_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "OrderPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Method = table.Column<string>(type: "longtext", nullable: false),
                    Reference = table.Column<string>(type: "longtext", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderPayments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "OrderProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreeIssueQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderProducts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_ChequeNumber",
                table: "Cheques",
                column: "ChequeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_OrderId",
                table: "Cheques",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_ShopId_ChequeDate",
                table: "Cheques",
                columns: new[] { "ShopId", "ChequeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Cheques_Status_ChequeDate",
                table: "Cheques",
                columns: new[] { "Status", "ChequeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Code",
                table: "Companies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ProductId_TransactionDate",
                table: "InventoryTransactions",
                columns: new[] { "ProductId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_OrderId",
                table: "OrderPayments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderProducts_OrderId_ProductId",
                table: "OrderProducts",
                columns: new[] { "OrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderProducts_ProductId",
                table: "OrderProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CompanyId",
                table: "Orders",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentStatus",
                table: "Orders",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SalesRepId_OrderDate",
                table: "Orders",
                columns: new[] { "SalesRepId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShopId_OrderDate",
                table: "Orders",
                columns: new[] { "ShopId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId_Sku",
                table: "Products",
                columns: new[] { "CompanyId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Shops_CompanyId_Code",
                table: "Shops",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shops_Name",
                table: "Shops",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StockInPayments_StockInId",
                table: "StockInPayments",
                column: "StockInId");

            migrationBuilder.CreateIndex(
                name: "IX_StockInProducts_ProductId",
                table: "StockInProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockInProducts_StockInId_ProductId",
                table: "StockInProducts",
                columns: new[] { "StockInId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_CompanyId_StockInDate",
                table: "StockIns",
                columns: new[] { "CompanyId", "StockInDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_PaymentStatus",
                table: "StockIns",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_StockInNumber",
                table: "StockIns",
                column: "StockInNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId",
                table: "Users",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cheques");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.DropTable(
                name: "OrderPayments");

            migrationBuilder.DropTable(
                name: "OrderProducts");

            migrationBuilder.DropTable(
                name: "StockInPayments");

            migrationBuilder.DropTable(
                name: "StockInProducts");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "StockIns");

            migrationBuilder.DropTable(
                name: "Shops");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}

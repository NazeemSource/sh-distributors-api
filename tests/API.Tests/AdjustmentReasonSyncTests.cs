using System.Text.Json;
using Distributor.Api.Contracts;
using Distributor.Api.Controllers;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class AdjustmentReasonSyncTests
{
    [Fact]
    public async Task Adjustment_reason_is_saved_and_available_to_another_reader()
    {
        var database = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(database).Options;
        Guid productId;
        Guid adminId;
        await using (var db = new AppDbContext(options))
        {
            var company = new Company { Code = "REASON-CO", Name = "Reason company" };
            var product = new Product { CompanyId = company.Id, Sku = "REASON-SKU", Barcode = "REASON-BAR", Name = "Test product", Category = "General" };
            var admin = new User { Name = "Admin", Username = "reason-admin", PasswordHash = "hash", Role = "Admin" };
            db.AddRange(company, product, admin);
            await db.SaveChangesAsync();
            productId = product.Id;
            adminId = admin.Id;
            var inventory = new InventoryService(db);
            var controller = new CatalogController(db, new OperationsService(db, inventory, new PaymentService(db)), inventory, new PaymentService(db));
            var result = await controller.Adjust(productId, new StockAdjustmentRequest(3, "IN", "Reason: Opening | stock | Date: 2026-10-01 | Unit cost: 10", "Opening | stock"));
            Assert.IsType<OkObjectResult>(result);
            Assert.IsType<OkObjectResult>(await controller.Adjust(productId, new StockAdjustmentRequest(1, "IN", "Reason: Opening | stock | Date: 2026-10-01 | Unit cost: 10", "Opening | stock")));
            Assert.Equal(4, await inventory.GetCurrentStock(productId));
        }
        await using (var db = new AppDbContext(options))
        {
            var response = await new SettingsController(db).AdjustmentReasons();
            Assert.Contains("Opening | stock", Assert.IsType<List<string>>(response));
            Assert.Single(await db.StockAdjustmentReasons.ToListAsync());
            var identity = new System.Security.Claims.ClaimsIdentity([
                new(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.ToString()),
                new(System.Security.Claims.ClaimTypes.Role, "Admin")
            ], "test");
            var snapshot = (JsonElement)await new OfflineData(db, new InventoryService(db), new PaymentService(db))
                .Snapshot(new System.Security.Claims.ClaimsPrincipal(identity));
            Assert.Equal("Opening | stock", snapshot.GetProperty("reads").GetProperty("/api/settings/adjustment-reasons")[0].GetString());
        }
    }

    [Fact]
    public async Task Stock_in_and_order_lists_do_not_drop_records_after_one_hundred()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var company = new Company { Code = "PAGED-CO", Name = "Paging company" };
        var shop = new Shop { CompanyId = company.Id, Code = "PAGED-SHOP", Name = "Paging shop" };
        var rep = new User { CompanyId = company.Id, Name = "Rep", Username = "paging-rep", PasswordHash = "hash", Role = "Rep" };
        db.AddRange(company, shop, rep);
        for (var index = 0; index < 101; index++)
        {
            db.StockIns.Add(new StockIn { CompanyId = company.Id, StockInNumber = $"GRN-{index}", StockInDate = new DateOnly(2026, 10, 1) });
            db.Orders.Add(new Order { CompanyId = company.Id, ShopId = shop.Id, SalesRepId = rep.Id, OrderNumber = $"INV-{index}", OrderDate = new DateOnly(2026, 10, 1), DeliveryDate = new DateOnly(2026, 10, 1) });
        }
        await db.SaveChangesAsync();
        var inventory = new InventoryService(db);
        var payments = new PaymentService(db);
        var controller = new OperationsController(db, new OperationsService(db, inventory, payments), payments)
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    [new(System.Security.Claims.ClaimTypes.Role, "Admin")], "test"))
            }}
        };
        Assert.Equal(101, JsonSerializer.SerializeToElement(await controller.StockIns(null, null, null, null)).GetArrayLength());
        Assert.Equal(101, JsonSerializer.SerializeToElement(await controller.Orders(null, null, null, null, null)).GetArrayLength());
    }
}

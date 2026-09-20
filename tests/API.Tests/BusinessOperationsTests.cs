using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class BusinessOperationsTests
{
    [Fact]
    public async Task Order_updates_inventory_and_payment_status_and_shop_outstanding()
    {
        await using var db = CreateDb();
        var company = new Company { Code = "COMPANY-01", Name = "Company - 01" };
        var shop = new Shop { CompanyId = company.Id, Code = "CUSTOMER-01", Name = "Customer - 01" };
        var rep = new User { CompanyId = company.Id, Name = "Rep - 01", Username = "rep01", PasswordHash = "hash", Role = "Rep" };
        db.AddRange(company, shop, rep); await db.SaveChangesAsync();
        var inventory = new InventoryService(db); var payments = new PaymentService(db); var operations = new OperationsService(db, inventory, payments);
        var product = await operations.CreateProduct(new ProductRequest(company.Id, "SKU-01", "10000001", "Product - 01", "General", 50, 30, 5, null, 100));

        var order = await operations.CreateOrder(new OrderRequest(shop.Id, rep.Id, "ORD-0001", new DateOnly(2026, 9, 19), new DateOnly(2026, 9, 20), "Address - 01", "", [new(product.Id, 10, 2, 50)]));

        Assert.Equal(88, await inventory.GetCurrentStock(product.Id));
        Assert.Equal(500, order.OrderTotal);
        Assert.Equal(500, await payments.GetShopOutstanding(shop.Id));
        await operations.AddOrderPayment(order.Id, new PaymentRequest(new DateOnly(2026, 9, 19), 200, "CASH", "PAY-01"));
        Assert.Equal("PARTIALLY_PAID", order.PaymentStatus);
        Assert.Equal(300, await payments.GetOrderBalance(order.Id));
        await operations.AddOrderPayment(order.Id, new PaymentRequest(new DateOnly(2026, 9, 19), 300, "BANK", "PAY-02"));
        Assert.Equal("PAID", order.PaymentStatus);
        Assert.Equal(0, await payments.GetShopOutstanding(shop.Id));
        db.ChangeTracker.Clear();
        var controller = new Distributor.Api.Controllers.OperationsController(db, operations, payments) {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new(System.Security.Claims.ClaimTypes.Role, "Admin")], "test"))
                }
            }
        };
        var json=System.Text.Json.JsonSerializer.Serialize(await controller.Orders(null,null,null,null,null),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var response=System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(2,response.RootElement[0].GetProperty("payments").GetArrayLength());
        Assert.Equal(500,response.RootElement[0].GetProperty("payments").EnumerateArray().Sum(x=>x.GetProperty("paidAmount").GetDecimal()));

        var checkOrder=await operations.CreateOrder(new OrderRequest(shop.Id,rep.Id,"ORD-CHECK",new DateOnly(2026,9,20),new DateOnly(2026,9,21),"","",[new(product.Id,2,0,50)]));
        await Assert.ThrowsAsync<BusinessException>(()=>operations.AddOrderPayment(checkOrder.Id,new PaymentRequest(new DateOnly(2026,9,20),50,"Check","CHK-01")));
        Assert.Equal(0,await payments.GetOrderPaidAmount(checkOrder.Id));
        await operations.AddOrderPayment(checkOrder.Id,new PaymentRequest(new DateOnly(2026,9,20),50,"Check","CHK-01","Bank - 01",new DateOnly(2026,9,22)));
        Assert.Equal(50,await payments.GetOrderPaidAmount(checkOrder.Id));
        Assert.Single(await db.Cheques.Where(x=>x.OrderId==checkOrder.Id).ToListAsync());
        await Assert.ThrowsAsync<BusinessException>(()=>operations.AddOrderPayment(checkOrder.Id,new PaymentRequest(new DateOnly(2026,9,20),10,"Check","CHK-01","Bank - 01",new DateOnly(2026,9,22))));
        Assert.Equal(50,await payments.GetOrderPaidAmount(checkOrder.Id));
    }

    [Fact]
    public async Task Stock_in_updates_inventory_and_company_balance()
    {
        await using var db = CreateDb();
        var company = new Company { Code = "COMPANY-02", Name = "Company - 02" }; db.Add(company); await db.SaveChangesAsync();
        var inventory = new InventoryService(db); var payments = new PaymentService(db); var operations = new OperationsService(db, inventory, payments);
        var product = await operations.CreateProduct(new ProductRequest(company.Id, "SKU-02", "10000002", "Product - 02", "General", 60, 40, 5, null));
        var stockIn = await operations.CreateStockIn(new StockInRequest(company.Id, "SIN-0001", new DateOnly(2026, 9, 19), "", [new(product.Id, 25, 40)]));

        Assert.Equal(25, await inventory.GetCurrentStock(product.Id));
        Assert.Equal(1000, await payments.GetCompanyOutstanding(company.Id));
        await operations.AddStockInPayment(stockIn.Id, new PaymentRequest(new DateOnly(2026, 9, 19), 1000, "BANK", "PAY-03"));
        Assert.Equal("PAID", stockIn.PaymentStatus);
        Assert.Equal(0, await payments.GetCompanyOutstanding(company.Id));
    }

    [Fact]
    public async Task Soft_deleted_records_are_filtered()
    {
        await using var db = CreateDb();
        db.Companies.Add(new Company { Code = "COMPANY-03", Name = "Company - 03", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        Assert.Empty(await db.Companies.ToListAsync());
        Assert.Single(await db.Companies.IgnoreQueryFilters().ToListAsync());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }
}

using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class BusinessOperationsTests
{
    [Fact]
    public async Task New_invoice_numbers_use_a_shared_daily_sequence()
    {
        await using var db=CreateDb();
        var company=new Company { Code="INVOICE-CO",Name="Invoice company" };
        var shop=new Shop { CompanyId=company.Id,Code="INVOICE-SHOP",Name="Invoice shop" };
        var rep=new User { CompanyId=company.Id,Name="Invoice rep",Username="invoice-rep",PasswordHash="hash",Role="Rep" };
        db.AddRange(company,shop,rep);await db.SaveChangesAsync();
        var operations=new OperationsService(db,new InventoryService(db),new PaymentService(db));
        var product=await operations.CreateProduct(new ProductRequest(company.Id,"INVOICE-SKU","90000001","Invoice product","General",10,5,1,null,20));
        var date=new DateOnly(2026,9,26);
        OrderRequest Request(DateOnly on)=>new(shop.Id,rep.Id,"INV-260920260001",on,on,"","",[new(product.Id,1,0,10)]);
        var first=await operations.CreateOrder(Request(date));
        var second=await operations.CreateOrder(Request(date));
        var nextDay=await operations.CreateOrder(Request(date.AddDays(1)));
        Assert.Equal("INV-260920260001",first.OrderNumber);
        Assert.Equal("INV-260920260002",second.OrderNumber);
        Assert.Equal("INV-270920260001",nextDay.OrderNumber);
    }

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
        var firstPayment=await db.OrderPayments.SingleAsync(x=>x.OrderId==order.Id&&x.Reference=="PAY-01");
        await operations.DeleteOrderPayment(order.Id,firstPayment.Id);
        Assert.Equal(200,await payments.GetOrderBalance(order.Id));
        Assert.Equal("PARTIALLY_PAID",(await db.Orders.SingleAsync(x=>x.Id==order.Id)).PaymentStatus);
        Assert.Single(await db.OrderPayments.Where(x=>x.OrderId==order.Id).ToListAsync());
        await Assert.ThrowsAsync<BusinessException>(()=>operations.DeleteOrderPayment(order.Id,firstPayment.Id));

        var checkOrder=await operations.CreateOrder(new OrderRequest(shop.Id,rep.Id,"ORD-CHECK",new DateOnly(2026,9,20),new DateOnly(2026,9,21),"","",[new(product.Id,2,0,50)]));
        await Assert.ThrowsAsync<BusinessException>(()=>operations.AddOrderPayment(checkOrder.Id,new PaymentRequest(new DateOnly(2026,9,20),50,"Check","CHK-01")));
        Assert.Equal(0,await payments.GetOrderPaidAmount(checkOrder.Id));
        var checkPayment=await operations.AddOrderPayment(checkOrder.Id,new PaymentRequest(new DateOnly(2026,9,20),50,"Check","CHK-01","Bank - 01",new DateOnly(2026,9,22)));
        Assert.Equal(50,await payments.GetOrderPaidAmount(checkOrder.Id));
        Assert.Single(await db.Cheques.Where(x=>x.OrderId==checkOrder.Id).ToListAsync());
        await Assert.ThrowsAsync<BusinessException>(()=>operations.AddOrderPayment(checkOrder.Id,new PaymentRequest(new DateOnly(2026,9,20),10,"Check","CHK-01","Bank - 01",new DateOnly(2026,9,22))));
        Assert.Equal(50,await payments.GetOrderPaidAmount(checkOrder.Id));
        await operations.DeleteOrderPayment(checkOrder.Id,checkPayment.Id);
        Assert.Equal(0,await payments.GetOrderPaidAmount(checkOrder.Id));
        Assert.Empty(await db.Cheques.Where(x=>x.OrderId==checkOrder.Id).ToListAsync());
        await operations.DeleteOrder(checkOrder.Id);
        Assert.Equal(88,await inventory.GetCurrentStock(product.Id));
        Assert.Empty(await db.Orders.Where(x=>x.Id==checkOrder.Id).ToListAsync());
        Assert.Empty(await db.Cheques.Where(x=>x.OrderId==checkOrder.Id).ToListAsync());
    }

    [Fact]
    public async Task Completing_selected_orders_allocates_quantities_and_tracks_partial_delivery()
    {
        await using var db = CreateDb();
        var company = new Company { Code = "DELIVERY-CO", Name = "Delivery company" };
        var shop = new Shop { CompanyId = company.Id, Code = "DELIVERY-SHOP", Name = "Delivery shop" };
        var rep = new User { CompanyId = company.Id, Name = "Delivery rep", Username = "deliveryrep", PasswordHash = "hash", Role = "Rep" };
        db.AddRange(company, shop, rep); await db.SaveChangesAsync();
        var inventory = new InventoryService(db); var operations = new OperationsService(db, inventory, new PaymentService(db));
        var product = await operations.CreateProduct(new ProductRequest(company.Id, "DELIVERY-SKU", "20000001", "Delivery product", "General", 10, 5, 1, null, 30));
        var first = await operations.CreateOrder(new OrderRequest(shop.Id, rep.Id, "DELIVERY-1", new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 20), "", "", [new(product.Id, 4, 0, 10)]));
        var second = await operations.CreateOrder(new OrderRequest(shop.Id, rep.Id, "DELIVERY-2", new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 21), "", "", [new(product.Id, 6, 0, 10)]));
        await Assert.ThrowsAsync<BusinessException>(() => operations.CompleteOrders(new CompleteOrdersRequest([first.Id, second.Id], new DateOnly(2026, 9, 22), [new(product.Id, 11)])));
        await operations.CompleteOrders(new CompleteOrdersRequest([first.Id, second.Id], new DateOnly(2026, 9, 22), [new(product.Id, 7)]));
        Assert.Equal("DELIVERED", first.Status);
        Assert.Equal(4, first.Products.Single().DeliveredQuantity);
        Assert.Equal("PARTIALLY_DELIVERED", second.Status);
        Assert.Equal(3, second.Products.Single().DeliveredQuantity);
        Assert.Equal(new DateOnly(2026, 9, 22), second.DeliveryDate);
        await Assert.ThrowsAsync<BusinessException>(() => operations.DeleteOrder(first.Id));
        await operations.CompleteOrders(new CompleteOrdersRequest([second.Id], new DateOnly(2026, 9, 23), [new(product.Id, 3)]));
        Assert.Equal("DELIVERED", second.Status);
        Assert.Equal(6, second.Products.Single().DeliveredQuantity);
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

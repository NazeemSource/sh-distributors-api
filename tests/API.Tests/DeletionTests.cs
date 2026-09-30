using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class DeletionTests
{
    [Fact]
    public async Task Delete_supplier_payment_reopens_balance_and_stock_in_deletion_reverses_stock()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = new Company { Code="DEL",Name="Deletion test" };db.Add(company);await db.SaveChangesAsync();
        var inventory = new InventoryService(db);var payments = new PaymentService(db);var service = new OperationsService(db,inventory,payments);
        var product = await service.CreateProduct(new ProductRequest(company.Id,"DEL","DEL","Test","",10,5,0,null));
        var stock = await service.CreateStockIn(new StockInRequest(company.Id,"DEL-STOCK",new DateOnly(2026,10,1),"",[new(product.Id,10,5)]));
        var payment = await service.AddStockInPayment(stock.Id,new PaymentRequest(new DateOnly(2026,10,1),50,"Cash",""));
        await service.DeleteStockInPayment(stock.Id,payment.Id);
        Assert.Equal(50,await payments.GetStockInBalance(stock.Id));Assert.Equal("PENDING",stock.PaymentStatus);
        await inventory.CreateStockAdjustment(product.Id,1,"OUT","");await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessException>(()=>service.DeleteStockIn(stock.Id));
        Assert.False(stock.IsDeleted);Assert.Equal(9,await inventory.GetCurrentStock(product.Id));
        await inventory.CreateStockAdjustment(product.Id,1,"IN","");await db.SaveChangesAsync();
        await service.DeleteStockIn(stock.Id);
        Assert.Equal(0,await inventory.GetCurrentStock(product.Id));Assert.Empty(await db.StockIns.ToListAsync());Assert.Empty(await db.StockInProducts.ToListAsync());
        Assert.Single(await db.StockIns.IgnoreQueryFilters().ToListAsync());
    }
}

using Distributor.Api.Data;
using Distributor.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Services;

public sealed class PaymentService(AppDbContext db)
{
    public Task<decimal> GetOrderPaidAmount(Guid orderId) => db.OrderPayments.Where(x => x.OrderId == orderId).SumAsync(x => x.PaidAmount);
    public async Task<decimal> GetOrderBalance(Guid orderId)
    {
        var order = await db.Orders.FindAsync(orderId) ?? throw new BusinessException("not_found", "Order was not found.", 404);
        return Math.Max(0, order.OrderTotal - await GetOrderPaidAmount(orderId));
    }
    public async Task RecalculateOrderPaymentStatus(Order order)
    {
        var paid = await GetOrderPaidAmount(order.Id);
        order.PaymentStatus = Status(paid, order.OrderTotal);
    }
    public Task<decimal> GetStockInPaidAmount(Guid stockInId) => db.StockInPayments.Where(x => x.StockInId == stockInId).SumAsync(x => x.PaidAmount);
    public async Task<decimal> GetStockInBalance(Guid stockInId)
    {
        var stockIn = await db.StockIns.FindAsync(stockInId) ?? throw new BusinessException("not_found", "Stock-in was not found.", 404);
        return Math.Max(0, stockIn.StockTotal - await GetStockInPaidAmount(stockInId));
    }
    public async Task RecalculateStockInPaymentStatus(StockIn stockIn) => stockIn.PaymentStatus = Status(await GetStockInPaidAmount(stockIn.Id), stockIn.StockTotal);
    public async Task<decimal> GetShopOutstanding(Guid shopId) => (await GetShopOutstandingOrders(shopId)).Sum(x => x.Balance);
    public async Task<List<OutstandingOrder>> GetShopOutstandingOrders(Guid shopId)
    {
        var orders = await db.Orders.AsNoTracking().Where(x => x.ShopId == shopId).ToListAsync();
        var result = new List<OutstandingOrder>();
        foreach (var order in orders) { var balance = await GetOrderBalance(order.Id); if (balance > 0) result.Add(new(order.Id, order.OrderNumber, order.OrderDate, order.OrderTotal, balance)); }
        return result;
    }
    public async Task<decimal> GetCompanyOutstanding(Guid companyId) => (await GetCompanyOutstandingStockIns(companyId)).Sum(x => x.Balance);
    public async Task<List<OutstandingStockIn>> GetCompanyOutstandingStockIns(Guid companyId)
    {
        var stockIns = await db.StockIns.AsNoTracking().Where(x => x.CompanyId == companyId).ToListAsync();
        var result = new List<OutstandingStockIn>();
        foreach (var stockIn in stockIns) { var balance = await GetStockInBalance(stockIn.Id); if (balance > 0) result.Add(new(stockIn.Id, stockIn.StockInNumber, stockIn.StockInDate, stockIn.StockTotal, balance)); }
        return result;
    }
    private static string Status(decimal paid, decimal total) => paid <= 0 ? "PENDING" : paid < total ? "PARTIALLY_PAID" : "PAID";
}
public sealed record OutstandingOrder(Guid Id, string Number, DateOnly Date, decimal Total, decimal Balance);
public sealed record OutstandingStockIn(Guid Id, string Number, DateOnly Date, decimal Total, decimal Balance);


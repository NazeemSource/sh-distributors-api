using Distributor.Api.Data;
using Distributor.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Services;

public sealed class InventoryService(AppDbContext db)
{
    public async Task<decimal> GetCurrentStock(Guid productId) => await db.InventoryTransactions.Where(x => x.ProductId == productId).SumAsync(x => x.QuantityIn - x.QuantityOut);
    public Task<List<InventoryTransaction>> GetStockHistory(Guid productId) => db.InventoryTransactions.AsNoTracking().Where(x => x.ProductId == productId).OrderByDescending(x => x.TransactionDate).ToListAsync();
    public async Task<bool> CheckAvailableStock(Guid productId, decimal requiredQuantity) => requiredQuantity > 0 && await GetCurrentStock(productId) >= requiredQuantity;

    public async Task CreateOpeningStock(Guid productId, decimal quantity)
    {
        if (quantity <= 0) return;
        if (await db.InventoryTransactions.AnyAsync(x => x.ProductId == productId)) throw new BusinessException("opening_stock_exists", "Opening stock can only be created before other stock movements.");
        db.InventoryTransactions.Add(Movement(productId, "OPENING_STOCK", quantity, 0, "PRODUCT", productId));
    }

    public void ProcessStockIn(StockIn stockIn)
    {
        foreach (var line in stockIn.Products) db.InventoryTransactions.Add(Movement(line.ProductId, "STOCK_IN", line.Quantity, 0, "STOCK_IN", stockIn.Id));
    }

    public void ProcessOrderStock(Order order)
    {
        foreach (var line in order.Products)
        {
            db.InventoryTransactions.Add(Movement(line.ProductId, "ORDER", 0, line.Quantity, "ORDER", order.Id));
            if (line.FreeIssueQuantity > 0) db.InventoryTransactions.Add(Movement(line.ProductId, "FREE_ISSUE", 0, line.FreeIssueQuantity, "ORDER", order.Id));
        }
    }

    public async Task<InventoryTransaction> CreateStockAdjustment(Guid productId, decimal quantity, string direction, string notes)
    {
        if (quantity <= 0) throw new BusinessException("invalid_quantity", "Quantity must be greater than zero.");
        var incoming = direction.Equals("IN", StringComparison.OrdinalIgnoreCase);
        if (!incoming && !direction.Equals("OUT", StringComparison.OrdinalIgnoreCase)) throw new BusinessException("invalid_direction", "Direction must be IN or OUT.");
        if (!incoming && !await CheckAvailableStock(productId, quantity)) throw new BusinessException("insufficient_stock", "The adjustment exceeds available stock.", 409);
        var movement = Movement(productId, "ADJUSTMENT", incoming ? quantity : 0, incoming ? 0 : quantity, "ADJUSTMENT", null, notes);
        db.InventoryTransactions.Add(movement);
        return movement;
    }

    private static InventoryTransaction Movement(Guid productId, string type, decimal quantityIn, decimal quantityOut, string referenceType, Guid? referenceId, string notes = "") =>
        new() { ProductId = productId, Type = type, QuantityIn = quantityIn, QuantityOut = quantityOut, ReferenceType = referenceType, ReferenceId = referenceId, Notes = notes };
}


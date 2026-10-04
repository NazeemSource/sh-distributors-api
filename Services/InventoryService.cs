using Distributor.Api.Data;
using Distributor.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Services;

public sealed class InventoryService(AppDbContext db)
{
    public async Task<decimal> GetCurrentStock(Guid productId) => await db.InventoryTransactions.Where(x => x.ProductId == productId).SumAsync(x => x.QuantityIn - x.QuantityOut);
    public Task<List<InventoryTransaction>> GetStockHistory(Guid productId) => db.InventoryTransactions.AsNoTracking().Where(x => x.ProductId == productId).OrderByDescending(x => x.TransactionDate).ToListAsync();
    public async Task<bool> CheckAvailableStock(Guid productId, decimal requiredQuantity) => requiredQuantity > 0 && await GetCurrentStock(productId) >= requiredQuantity;

    public async Task CreateOpeningStock(Guid productId, decimal quantity, decimal unitCost, decimal unitPrice)
    {
        if (quantity <= 0) return;
        if (await db.InventoryTransactions.AnyAsync(x => x.ProductId == productId)) throw new BusinessException("opening_stock_exists", "Opening stock can only be created before other stock movements.");
        db.InventoryTransactions.Add(Movement(productId, "OPENING_STOCK", quantity, 0, "PRODUCT", productId, "Opening stock", unitCost, unitPrice));
    }

    public void ProcessStockIn(StockIn stockIn, IReadOnlyDictionary<Guid, Product> products)
    {
        foreach (var line in stockIn.Products) db.InventoryTransactions.Add(Movement(line.ProductId, "STOCK_IN", line.Quantity, 0, "STOCK_IN", stockIn.Id, "", line.UnitCost, products[line.ProductId].SellingPrice));
    }

    public void ProcessOrderStock(Order order)
    {
        foreach (var line in order.Products)
        {
            db.InventoryTransactions.Add(Movement(line.ProductId, "ORDER", 0, line.Quantity, "ORDER", order.Id, "", null, line.UnitPrice));
            if (line.FreeIssueQuantity > 0) db.InventoryTransactions.Add(Movement(line.ProductId, "FREE_ISSUE", 0, line.FreeIssueQuantity, "ORDER", order.Id, "", null, 0));
            line.StockCommittedQuantity = line.Quantity + line.FreeIssueQuantity;
        }
    }

    public void ProcessOrderDelivery(Order order, OrderProduct line, decimal quantity)
    {
        if (quantity <= 0) return;
        db.InventoryTransactions.Add(Movement(line.ProductId, "ORDER_DELIVERY", 0, quantity, "ORDER", order.Id, "", null, line.UnitPrice));
        line.StockCommittedQuantity += quantity;
    }

    public void ReverseOrderStock(Order order)
    {
        foreach (var line in order.Products)
        {
            if (line.StockCommittedQuantity > 0)
                db.InventoryTransactions.Add(Movement(line.ProductId, "ORDER_EDIT_REVERSAL", line.StockCommittedQuantity, 0, "ORDER", order.Id, "", null, line.UnitPrice));
        }
    }

    public async Task<InventoryTransaction> CreateStockAdjustment(Guid productId, decimal quantity, string direction, string notes, decimal? unitCost = null, decimal? unitPrice = null)
    {
        if (quantity <= 0) throw new BusinessException("invalid_quantity", "Quantity must be greater than zero.");
        var incoming = direction.Equals("IN", StringComparison.OrdinalIgnoreCase);
        if (!incoming && !direction.Equals("OUT", StringComparison.OrdinalIgnoreCase)) throw new BusinessException("invalid_direction", "Direction must be IN or OUT.");
        if (!incoming && !await CheckAvailableStock(productId, quantity)) throw new BusinessException("insufficient_stock", "The adjustment exceeds available stock.", 409);
        var movement = Movement(productId, "ADJUSTMENT", incoming ? quantity : 0, incoming ? 0 : quantity, "ADJUSTMENT", null, notes, unitCost, unitPrice);
        db.InventoryTransactions.Add(movement);
        return movement;
    }

    public async Task<InventoryTransaction> UpdateManualMovement(Guid productId, Guid movementId, DateOnly date, decimal signedQuantity, string reason, decimal unitCost, decimal unitPrice)
    {
        var movement = await EditableMovement(productId, movementId);
        if (signedQuantity == 0) throw new BusinessException("invalid_quantity", "Quantity cannot be zero.");
        if (movement.Type == "OPENING_STOCK" && signedQuantity < 0) throw new BusinessException("invalid_quantity", "Opening stock must be greater than zero.");
        var current = await GetCurrentStock(productId);
        var original = movement.QuantityIn - movement.QuantityOut;
        if (current - original + signedQuantity < 0) throw new BusinessException("insufficient_stock", "This correction would make stock negative.", 409);
        movement.TransactionDate = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        movement.QuantityIn = Math.Max(0, signedQuantity);
        movement.QuantityOut = Math.Max(0, -signedQuantity);
        movement.UnitCost = unitCost;
        movement.UnitPrice = unitPrice;
        movement.Notes = reason.Trim();
        movement.UpdatedAt = DateTimeOffset.UtcNow;
        return movement;
    }

    public async Task DeleteManualMovement(Guid productId, Guid movementId)
    {
        var movement = await EditableMovement(productId, movementId);
        var current = await GetCurrentStock(productId);
        if (current - (movement.QuantityIn - movement.QuantityOut) < 0) throw new BusinessException("insufficient_stock", "This movement cannot be deleted because it would make stock negative.", 409);
        movement.IsDeleted = true;
        movement.DeletedAt = movement.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task<InventoryTransaction> EditableMovement(Guid productId, Guid movementId)
    {
        var movement = await db.InventoryTransactions.SingleOrDefaultAsync(x => x.Id == movementId && x.ProductId == productId)
            ?? throw new BusinessException("not_found", "Stock movement was not found.", 404);
        if (movement.Type is not ("ADJUSTMENT" or "OPENING_STOCK"))
            throw new BusinessException("movement_locked", "Delivered orders and Stock In movements must be corrected from their source document.", 409);
        return movement;
    }

    private static InventoryTransaction Movement(Guid productId, string type, decimal quantityIn, decimal quantityOut, string referenceType, Guid? referenceId, string notes = "", decimal? unitCost = null, decimal? unitPrice = null) =>
        new() { ProductId = productId, Type = type, QuantityIn = quantityIn, QuantityOut = quantityOut, UnitCost = unitCost, UnitPrice = unitPrice, ReferenceType = referenceType, ReferenceId = referenceId, Notes = notes };
}

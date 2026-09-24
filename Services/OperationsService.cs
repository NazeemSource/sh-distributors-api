using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Distributor.Api.Services;

public sealed class OperationsService(AppDbContext db, InventoryService inventory, PaymentService payments)
{
    public async Task<Product> CreateProduct(ProductRequest request)
    {
        if (request.SellingPrice < 0 || request.CostPrice < 0 || request.OpeningStock < 0) throw new BusinessException("validation_error", "Prices and opening stock cannot be negative.");
        if (!await db.Companies.AnyAsync(x => x.Id == request.CompanyId)) throw new BusinessException("not_found", "Company was not found.", 404);
        if (await db.Products.AnyAsync(x => x.Barcode == request.Barcode || x.CompanyId == request.CompanyId && x.Sku == request.Sku)) throw new BusinessException("duplicate_product", "SKU or barcode already exists.", 409);
        await using var transaction = await BeginTransaction();
        try
        {
            var product = new Product { CompanyId=request.CompanyId, Sku=request.Sku.Trim(), Barcode=request.Barcode.Trim(), Name=request.Name.Trim(), Category=request.Category.Trim(), SellingPrice=request.SellingPrice, CostPrice=request.CostPrice, ReorderLevel=request.ReorderLevel, ExpiryDate=request.ExpiryDate, Active=request.Active };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            await inventory.CreateOpeningStock(product.Id, request.OpeningStock);
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return product;
        }
        catch { if (transaction is not null) await transaction.RollbackAsync(); throw; }
    }

    public async Task<StockIn> CreateStockIn(StockInRequest request)
    {
        if (request.Products.Count == 0) throw new BusinessException("validation_error", "At least one product is required.");
        if (!await db.Companies.AnyAsync(x => x.Id == request.CompanyId)) throw new BusinessException("not_found", "Company was not found.", 404);
        if (await db.StockIns.AnyAsync(x => x.StockInNumber == request.StockInNumber)) throw new BusinessException("duplicate_stock_in", "Stock-in number already exists.", 409);
        if (request.Products.GroupBy(x => x.ProductId).Any(x => x.Count() > 1)) throw new BusinessException("duplicate_product", "A product can appear only once.");
        var products = await db.Products.Where(x => request.Products.Select(y => y.ProductId).Contains(x.Id) && x.CompanyId == request.CompanyId && x.Active).ToDictionaryAsync(x => x.Id);
        if (products.Count != request.Products.Count) throw new BusinessException("invalid_product", "One or more products are invalid.");
        if (request.Products.Any(x => x.Quantity <= 0 || x.UnitCost < 0)) throw new BusinessException("validation_error", "Quantities must be positive and costs cannot be negative.");
        await using var transaction = await BeginTransaction();
        try
        {
            var stockIn = new StockIn { CompanyId=request.CompanyId, StockInNumber=request.StockInNumber.Trim(), StockInDate=request.StockInDate, Notes=request.Notes.Trim(), Products=request.Products.Select(x => new StockInProduct { ProductId=x.ProductId, Quantity=x.Quantity, UnitCost=x.UnitCost, LineTotal=decimal.Round(x.Quantity*x.UnitCost,2) }).ToList() };
            stockIn.StockTotal = stockIn.Products.Sum(x => x.LineTotal);
            db.StockIns.Add(stockIn); inventory.ProcessStockIn(stockIn);
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return stockIn;
        }
        catch { if (transaction is not null) await transaction.RollbackAsync(); throw; }
    }

    public async Task<Order> CreateOrder(OrderRequest request)
    {
        if (request.Products.Count == 0) throw new BusinessException("validation_error", "At least one product is required.");
        if (request.DeliveryDate < request.OrderDate) throw new BusinessException("invalid_date", "Delivery date cannot be before order date.");
        var shop = await db.Shops.FindAsync(request.ShopId) ?? throw new BusinessException("not_found", "Shop was not found.", 404);
        var rep = await db.Users.FindAsync(request.SalesRepId) ?? throw new BusinessException("not_found", "Sales rep was not found.", 404);
        if (rep.Role != "Rep" || rep.CompanyId != shop.CompanyId) throw new BusinessException("invalid_sales_rep", "Sales rep is not assigned to the shop company.");
        if (await db.Orders.AnyAsync(x => x.OrderNumber == request.OrderNumber)) throw new BusinessException("duplicate_order", "Order number already exists.", 409);
        if (request.Products.GroupBy(x => x.ProductId).Any(x => x.Count() > 1)) throw new BusinessException("duplicate_product", "A product can appear only once.");
        var products = await db.Products.Where(x => request.Products.Select(y => y.ProductId).Contains(x.Id) && x.CompanyId == shop.CompanyId && x.Active).ToDictionaryAsync(x => x.Id);
        if (products.Count != request.Products.Count) throw new BusinessException("invalid_product", "One or more products are invalid.");
        foreach (var line in request.Products)
        {
            if (line.Quantity <= 0 || line.FreeIssueQuantity < 0 || line.UnitPrice < 0) throw new BusinessException("validation_error", "Quantities and prices are invalid.");
            if (!await inventory.CheckAvailableStock(line.ProductId, line.Quantity + line.FreeIssueQuantity)) throw new BusinessException("insufficient_stock", $"Insufficient stock for {products[line.ProductId].Name}.", 409);
        }
        await using var transaction = await BeginTransaction();
        try
        {
            var order = new Order { CompanyId=shop.CompanyId, ShopId=shop.Id, SalesRepId=rep.Id, OrderNumber=request.OrderNumber.Trim(), OrderDate=request.OrderDate, DeliveryDate=request.DeliveryDate, DeliveryAddress=request.DeliveryAddress.Trim(), Notes=request.Notes.Trim(), Products=request.Products.Select(x => new OrderProduct { ProductId=x.ProductId, Quantity=x.Quantity, FreeIssueQuantity=x.FreeIssueQuantity, UnitPrice=x.UnitPrice, LineSubtotal=decimal.Round(x.Quantity*x.UnitPrice,2) }).ToList() };
            order.OrderTotal = order.Products.Sum(x => x.LineSubtotal);
            db.Orders.Add(order); inventory.ProcessOrderStock(order);
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return order;
        }
        catch { if (transaction is not null) await transaction.RollbackAsync(); throw; }
    }

    public async Task<Order> UpdateOrder(Guid id, OrderRequest request)
    {
        if (request.Products.Count == 0) throw new BusinessException("validation_error", "At least one product is required.");
        if (request.DeliveryDate < request.OrderDate) throw new BusinessException("invalid_date", "Delivery date cannot be before order date.");
        if (request.Products.GroupBy(x => x.ProductId).Any(x => x.Count() > 1)) throw new BusinessException("duplicate_product", "A product can appear only once.");
        var order = await db.Orders.Include(x => x.Products).SingleOrDefaultAsync(x => x.Id == id) ?? throw new BusinessException("not_found", "Order was not found.", 404);
        if (order.Products.Any(x => x.DeliveredQuantity > 0)) throw new BusinessException("already_delivered", "Delivered orders cannot be edited.", 409);
        var shop = await db.Shops.FindAsync(request.ShopId) ?? throw new BusinessException("not_found", "Shop was not found.", 404);
        var rep = await db.Users.FindAsync(request.SalesRepId) ?? throw new BusinessException("not_found", "Sales rep was not found.", 404);
        if (rep.Role != "Rep" || rep.CompanyId != shop.CompanyId) throw new BusinessException("invalid_sales_rep", "Sales rep is not assigned to the shop company.");
        if (await db.Orders.AnyAsync(x => x.Id != id && x.OrderNumber == request.OrderNumber)) throw new BusinessException("duplicate_order", "Order number already exists.", 409);
        var products = await db.Products.Where(x => request.Products.Select(y => y.ProductId).Contains(x.Id) && x.CompanyId == shop.CompanyId && x.Active).ToDictionaryAsync(x => x.Id);
        if (products.Count != request.Products.Count) throw new BusinessException("invalid_product", "One or more products are invalid.");
        foreach (var line in request.Products)
        {
            if (line.Quantity <= 0 || line.FreeIssueQuantity < 0 || line.UnitPrice < 0) throw new BusinessException("validation_error", "Quantities and prices are invalid.");
            var returnedQuantity = order.Products.Where(x => x.ProductId == line.ProductId).Sum(x => x.Quantity + x.FreeIssueQuantity);
            if (await inventory.GetCurrentStock(line.ProductId) + returnedQuantity < line.Quantity + line.FreeIssueQuantity) throw new BusinessException("insufficient_stock", $"Insufficient stock for {products[line.ProductId].Name}.", 409);
        }
        var newTotal = request.Products.Sum(x => decimal.Round(x.Quantity * x.UnitPrice, 2));
        if (await payments.GetOrderPaidAmount(id) > newTotal) throw new BusinessException("invalid_total", "The updated total cannot be lower than payments already received.");
        await using var transaction = await BeginTransaction();
        try
        {
            inventory.ReverseOrderStock(order);
            db.OrderProducts.RemoveRange(order.Products);
            order.CompanyId=shop.CompanyId;order.ShopId=shop.Id;order.SalesRepId=rep.Id;order.OrderNumber=request.OrderNumber.Trim();order.OrderDate=request.OrderDate;order.DeliveryDate=request.DeliveryDate;order.DeliveryAddress=request.DeliveryAddress.Trim();order.Notes=request.Notes.Trim();
            order.Products=request.Products.Select(x=>new OrderProduct { ProductId=x.ProductId,Quantity=x.Quantity,FreeIssueQuantity=x.FreeIssueQuantity,UnitPrice=x.UnitPrice,LineSubtotal=decimal.Round(x.Quantity*x.UnitPrice,2) }).ToList();
            order.OrderTotal=newTotal;inventory.ProcessOrderStock(order);
            await db.SaveChangesAsync();await payments.RecalculateOrderPaymentStatus(order);await db.SaveChangesAsync();
            if(transaction is not null)await transaction.CommitAsync();return order;
        }
        catch { if(transaction is not null)await transaction.RollbackAsync();throw; }
    }

    public async Task DeleteOrder(Guid id)
    {
        var order = await db.Orders.Include(x => x.Products).Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessException("not_found", "Order was not found.", 404);
        if (order.Products.Any(x => x.DeliveredQuantity > 0)) throw new BusinessException("already_delivered", "Delivered orders cannot be deleted.", 409);
        var cheques = await db.Cheques.Where(x => x.OrderId == id).ToListAsync();
        await using var transaction = await BeginTransaction();
        try
        {
            inventory.ReverseOrderStock(order);
            var now = DateTimeOffset.UtcNow;
            order.IsDeleted = true; order.DeletedAt = now; order.UpdatedAt = now;
            foreach (var line in order.Products) { line.IsDeleted = true; line.DeletedAt = now; line.UpdatedAt = now; }
            foreach (var payment in order.Payments) { payment.IsDeleted = true; payment.DeletedAt = now; payment.UpdatedAt = now; }
            foreach (var cheque in cheques) { cheque.IsDeleted = true; cheque.DeletedAt = now; cheque.UpdatedAt = now; }
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
        }
        catch { if (transaction is not null) await transaction.RollbackAsync(); throw; }
    }

    public async Task<List<Order>> CompleteOrders(CompleteOrdersRequest request)
    {
        if (request.OrderIds.Count == 0 || request.OrderIds.Distinct().Count() != request.OrderIds.Count)
            throw new BusinessException("invalid_orders", "Select one or more distinct orders.");
        if (request.Products.Count == 0 || request.Products.GroupBy(x => x.ProductId).Any(x => x.Count() > 1) || request.Products.Any(x => x.Quantity <= 0))
            throw new BusinessException("invalid_delivery", "Enter a positive delivery quantity for each selected product.");
        var orders = await db.Orders.Include(x => x.Products).Where(x => request.OrderIds.Contains(x.Id)).OrderBy(x => x.OrderDate).ThenBy(x => x.Id).ToListAsync();
        if (orders.Count != request.OrderIds.Count) throw new BusinessException("not_found", "One or more orders were not found.", 404);
        if (orders.Any(x => x.OrderDate > request.DeliveryDate)) throw new BusinessException("invalid_date", "Delivery date cannot be before an order date.");
        var remaining = orders.SelectMany(x => x.Products)
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.Quantity + line.FreeIssueQuantity - line.DeliveredQuantity));
        if (request.Products.Any(x => !remaining.TryGetValue(x.ProductId, out var available) || x.Quantity > available))
            throw new BusinessException("invalid_delivery", "Delivery quantity exceeds the selected orders' remaining quantity.");
        await using var transaction = await BeginTransaction();
        try
        {
            foreach (var delivery in request.Products)
            {
                var quantity = delivery.Quantity;
                foreach (var order in orders)
                foreach (var line in order.Products.Where(x => x.ProductId == delivery.ProductId))
                {
                    var available = line.Quantity + line.FreeIssueQuantity - line.DeliveredQuantity;
                    var allocated = Math.Min(available, quantity);
                    line.DeliveredQuantity += allocated;
                    quantity -= allocated;
                    if (quantity == 0) break;
                }
            }
            foreach (var order in orders)
            {
                var delivered = order.Products.Sum(x => x.DeliveredQuantity);
                order.Status = delivered == 0 ? "CONFIRMED" : order.Products.All(x => x.DeliveredQuantity == x.Quantity + x.FreeIssueQuantity) ? "DELIVERED" : "PARTIALLY_DELIVERED";
                if (delivered > 0) order.DeliveryDate = request.DeliveryDate;
                order.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return orders;
        }
        catch { if (transaction is not null) await transaction.RollbackAsync(); throw; }
    }

    public async Task<OrderPayment> AddOrderPayment(Guid orderId, PaymentRequest request)
    {
        if (request.PaidAmount <= 0) throw new BusinessException("invalid_payment", "Payment amount must be greater than zero.");
        var order = await db.Orders.FindAsync(orderId) ?? throw new BusinessException("not_found", "Order was not found.", 404);
        if (request.PaidAmount > await payments.GetOrderBalance(orderId)) throw new BusinessException("invalid_payment", "Payment exceeds the order balance.");
        var byCheque = string.Equals(request.Method, "Check", StringComparison.OrdinalIgnoreCase);
        if (byCheque && (string.IsNullOrWhiteSpace(request.BankName) || string.IsNullOrWhiteSpace(request.Reference) || request.ChequeDate is null))
            throw new BusinessException("invalid_cheque", "Bank, check number, and check date are required.");
        if (byCheque && await db.Cheques.AnyAsync(x => x.ChequeNumber == request.Reference.Trim()))
            throw new BusinessException("duplicate_cheque", "Check number already exists.", 409);
        await using var transaction = await BeginTransaction();
        try {
            var payment = new OrderPayment { OrderId=orderId, PaymentDate=request.PaymentDate, PaidAmount=request.PaidAmount, Method=request.Method.Trim(), Reference=request.Reference.Trim() };
            db.OrderPayments.Add(payment);
            if (byCheque) db.Cheques.Add(new Cheque { ShopId=order.ShopId, OrderId=order.Id, ChequeNumber=request.Reference.Trim(), BankName=request.BankName!.Trim(), Amount=request.PaidAmount, ChequeDate=request.ChequeDate!.Value, Status="PENDING", RemindBeforeDays=3 });
            await db.SaveChangesAsync(); await payments.RecalculateOrderPaymentStatus(order); await db.SaveChangesAsync(); if(transaction is not null)await transaction.CommitAsync(); return payment;
        }
        catch { if(transaction is not null)await transaction.RollbackAsync(); throw; }
    }

    public async Task<StockInPayment> AddStockInPayment(Guid stockInId, PaymentRequest request)
    {
        if (request.PaidAmount <= 0) throw new BusinessException("invalid_payment", "Payment amount must be greater than zero.");
        var stockIn = await db.StockIns.FindAsync(stockInId) ?? throw new BusinessException("not_found", "Stock-in was not found.", 404);
        if (request.PaidAmount > await payments.GetStockInBalance(stockInId)) throw new BusinessException("invalid_payment", "Payment exceeds the stock-in balance.");
        await using var transaction = await BeginTransaction();
        try { var payment = new StockInPayment { StockInId=stockInId, PaymentDate=request.PaymentDate, PaidAmount=request.PaidAmount, Method=request.Method.Trim(), Reference=request.Reference.Trim() }; db.StockInPayments.Add(payment); await db.SaveChangesAsync(); await payments.RecalculateStockInPaymentStatus(stockIn); await db.SaveChangesAsync(); if(transaction is not null)await transaction.CommitAsync(); return payment; }
        catch { if(transaction is not null)await transaction.RollbackAsync(); throw; }
    }

    private async Task<IDbContextTransaction?> BeginTransaction() => db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
}

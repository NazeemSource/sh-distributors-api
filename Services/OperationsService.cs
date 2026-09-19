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

    public async Task<OrderPayment> AddOrderPayment(Guid orderId, PaymentRequest request)
    {
        if (request.PaidAmount <= 0) throw new BusinessException("invalid_payment", "Payment amount must be greater than zero.");
        var order = await db.Orders.FindAsync(orderId) ?? throw new BusinessException("not_found", "Order was not found.", 404);
        if (request.PaidAmount > await payments.GetOrderBalance(orderId)) throw new BusinessException("invalid_payment", "Payment exceeds the order balance.");
        await using var transaction = await BeginTransaction();
        try { var payment = new OrderPayment { OrderId=orderId, PaymentDate=request.PaymentDate, PaidAmount=request.PaidAmount, Method=request.Method.Trim(), Reference=request.Reference.Trim() }; db.OrderPayments.Add(payment); await db.SaveChangesAsync(); await payments.RecalculateOrderPaymentStatus(order); await db.SaveChangesAsync(); if(transaction is not null)await transaction.CommitAsync(); return payment; }
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

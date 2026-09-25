using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Services;

public sealed class OfflineData(AppDbContext db, InventoryService inventory, PaymentService payments)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReferenceHandler = ReferenceHandler.IgnoreCycles };
    public async Task<string> Version(string key)
    {
        var parts = key.Split('/');
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return "missing";
        Entity? entity = parts[0] switch {
            "companies" => await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id),
            "shops" => await db.Shops.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id),
            "products" => await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id),
            "orders" => await db.Orders.AsNoTracking().Include(x => x.Products).Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id),
            "stock-ins" => await db.StockIns.AsNoTracking().Include(x => x.Products).Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id),
            "cheques" => await db.Cheques.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id), _ => null
        };
        if (entity is null) return "missing";
        // Sort children so query execution order cannot create a false conflict.
        if (entity is Order order) { order.Products = order.Products.OrderBy(x => x.Id).ToList(); order.Payments = order.Payments.OrderBy(x => x.Id).ToList(); }
        if (entity is StockIn stock) { stock.Products = stock.Products.OrderBy(x => x.Id).ToList(); stock.Payments = stock.Payments.OrderBy(x => x.Id).ToList(); }
        var json = JsonSerializer.Serialize(entity, entity.GetType(), Json);
        if (entity is Product) json += JsonSerializer.Serialize(await db.InventoryTransactions.AsNoTracking().Where(x => x.ProductId == id).OrderBy(x => x.Id).Select(x => new { x.Id, x.QuantityIn, x.QuantityOut }).ToListAsync(), Json);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public async Task<object> Snapshot(ClaimsPrincipal user)
    {
        var admin = user.IsAdmin(); var companyId = user.CompanyId(); var userId = user.UserId();
        var companies = await db.Companies.AsNoTracking().Where(x => admin || x.Id == companyId).OrderBy(x => x.Name).ToListAsync();
        var shops = await db.Shops.AsNoTracking().Where(x => admin || x.CompanyId == companyId).OrderBy(x => x.Name).ToListAsync();
        var products = await db.Products.AsNoTracking().Where(x => admin || x.CompanyId == companyId).OrderBy(x => x.Name).ToListAsync();
        var orders = await db.Orders.AsNoTracking().Where(x => admin || x.SalesRepId == userId).Include(x => x.Products).Include(x => x.Payments).OrderByDescending(x => x.OrderDate).ToListAsync();
        var stockIns = admin ? await db.StockIns.AsNoTracking().Include(x => x.Products).Include(x => x.Payments).OrderByDescending(x => x.StockInDate).ToListAsync() : [];
        var shopIds = shops.Select(x => x.Id).ToList();
        var cheques = await db.Cheques.AsNoTracking().Where(x => shopIds.Contains(x.ShopId)).ToListAsync();
        var users = await db.Users.AsNoTracking().Where(x => admin || x.Id == userId).ToListAsync();
        object Page<T>(List<T> items) => new { items, total = items.Count, page = 1, totalPages = 1 };
        var reads = new Dictionary<string, object> {
            ["/api/auth/me"] = UserView.From(users.Single(x => x.Id == userId)),
            ["/api/companies"] = Page(companies), ["/api/shops"] = Page(shops), ["/api/products"] = Page(products),
            ["/api/orders"] = orders, ["/api/stock-ins"] = stockIns, ["/api/cheques"] = Page(cheques)
        };
        if (admin) reads["/api/users"] = users.Select(UserView.From).ToList();
        foreach (var p in products) {
            reads[$"/api/products/{p.Id}/stock"] = new { productId = p.Id, currentStock = await inventory.GetCurrentStock(p.Id) };
            reads[$"/api/products/{p.Id}/stock-history"] = await inventory.GetStockHistory(p.Id);
        }
        foreach (var s in shops) reads[$"/api/shops/{s.Id}/outstanding"] = new { shopId = s.Id, outstanding = await payments.GetShopOutstanding(s.Id), orders = await payments.GetShopOutstandingOrders(s.Id) };
        if (admin) foreach (var c in companies) reads[$"/api/companies/{c.Id}/outstanding"] = new { companyId = c.Id, outstanding = await payments.GetCompanyOutstanding(c.Id), stockIns = await payments.GetCompanyOutstandingStockIns(c.Id) };
        var versions = new Dictionary<string, string>();
        foreach (var (resource, ids) in new[] { ("companies", companies.Select(x => x.Id)), ("shops", shops.Select(x => x.Id)), ("products", products.Select(x => x.Id)), ("orders", orders.Select(x => x.Id)), ("stock-ins", stockIns.Select(x => x.Id)), ("cheques", cheques.Select(x => x.Id)) })
            foreach (var id in ids) versions[$"{resource}/{id}"] = await Version($"{resource}/{id}");
        // Convert here to detach navigation properties and use the same JSON settings everywhere.
        return JsonSerializer.SerializeToElement(new { reads, versions, savedAt = DateTimeOffset.UtcNow }, Json);
    }
}

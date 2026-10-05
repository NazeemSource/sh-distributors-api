using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Extensions;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Controllers;

[ApiController, Route("api"), Authorize]
public sealed class CatalogController(AppDbContext db, OperationsService operations, InventoryService inventory, PaymentService payments) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<object> Companies([FromQuery]string? search="",[FromQuery]int page=1,[FromQuery]int pageSize=25) { var q=db.Companies.AsNoTracking().AsQueryable(); if(!User.IsAdmin()) q=q.Where(x=>x.Id==User.CompanyId()); if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Name.Contains(search)||x.Code.Contains(search)); return await Page(q.OrderBy(x=>x.Name),page,pageSize); }
    [HttpPost("companies"),Authorize(Roles="Admin")]
    public async Task<IActionResult> CreateCompany(CompanyRequest r){if(string.IsNullOrWhiteSpace(r.Code)||string.IsNullOrWhiteSpace(r.Name))return ValidationProblem("Code and name are required.");var code=r.Code.Trim();if(await db.Companies.AnyAsync(x=>x.Code==code))return Conflict(new{error="duplicate_company",message="Company code is already in use."});var x=new Company{Code=code,Name=r.Name.Trim(),ContactName=r.ContactName.Trim(),Phone=r.Phone.Trim(),Category=r.Category.Trim(),Address=r.Address.Trim(),City=r.City.Trim(),Active=r.Active};db.Add(x);await db.SaveChangesAsync();return Created($"/api/companies/{x.Id}",x);}
    [HttpPut("companies/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> UpdateCompany(Guid id,CompanyRequest r){var x=await db.Companies.FindAsync(id);if(x is null)return NotFound();var code=r.Code.Trim();if(await db.Companies.AnyAsync(other=>other.Id!=id&&other.Code==code))return Conflict(new{error="duplicate_company",message="Company code is already in use."});x.Code=code;x.Name=r.Name.Trim();x.ContactName=r.ContactName.Trim();x.Phone=r.Phone.Trim();x.Category=r.Category.Trim();x.Address=r.Address.Trim();x.City=r.City.Trim();x.Active=r.Active;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("companies/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> DeleteCompany(Guid id){
        if(await db.Products.AnyAsync(x=>x.CompanyId==id)||await db.Shops.AnyAsync(x=>x.CompanyId==id)||await db.Users.AnyAsync(x=>x.CompanyId==id)||await db.StockIns.AnyAsync(x=>x.CompanyId==id)||await db.Orders.AnyAsync(x=>x.CompanyId==id)||await db.DepositAccounts.AnyAsync(x=>x.CompanyId==id))return Conflict(new{message="Delete this company's linked products, shops, reps and accounts first."});
        return await SoftDelete(db.Companies,id);
    }

    [HttpGet("shops")]
    public async Task<object> Shops([FromQuery]Guid? companyId,[FromQuery]string? search="",[FromQuery]int page=1,[FromQuery]int pageSize=25){var q=db.Shops.AsNoTracking().AsQueryable();if(!User.IsAdmin()){var repId=User.UserId();q=q.Where(x=>x.CompanyId==User.CompanyId()&&(x.CreatedByRepId==null||x.CreatedByRepId==repId));}else if(companyId.HasValue)q=q.Where(x=>x.CompanyId==companyId);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Name.Contains(search)||x.Code.Contains(search));return await Page(q.OrderBy(x=>x.Name),page,pageSize);}
    [HttpGet("shops/{id:guid}/outstanding")]
    public async Task<IActionResult> ShopOutstanding(Guid id){if(!User.IsAdmin()&&!await db.Shops.AnyAsync(x=>x.Id==id&&x.CompanyId==User.CompanyId()&&(x.CreatedByRepId==null||x.CreatedByRepId==User.UserId())))return NotFound();return Ok(new{shopId=id,outstanding=await payments.GetShopOutstanding(id),orders=await payments.GetShopOutstandingOrders(id)});}
    [HttpPost("shops")]
    public async Task<IActionResult> CreateShop(ShopRequest r){if(!User.IsAdmin()&&r.CompanyId!=User.CompanyId())return Forbid();if(r.CreditLimit<0)return ValidationProblem("Credit limit cannot be negative.");if(!await db.Companies.AnyAsync(x=>x.Id==r.CompanyId))return NotFound();var x=new Shop{CompanyId=r.CompanyId,Code="",Name=r.Name.Trim(),ContactName=r.ContactName.Trim(),Phone=r.Phone.Trim(),Address=r.Address.Trim(),City=r.City.Trim(),CreditLimit=User.IsAdmin()?r.CreditLimit:0,Active=r.Active,CreatedByRepId=User.IsAdmin()?null:User.UserId()};var code=User.IsAdmin()?r.Code.Trim():$"REP-{x.Id:N}";if(await db.Shops.AnyAsync(other=>other.CompanyId==r.CompanyId&&other.Code==code))return Conflict(new{error="duplicate_shop",message="Shop code is already in use for this company."});x.Code=code;db.Add(x);await db.SaveChangesAsync();return Created($"/api/shops/{x.Id}",x);}
    [HttpPut("shops/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> UpdateShop(Guid id,ShopRequest r){var x=await db.Shops.FindAsync(id);if(x is null)return NotFound();if(!await db.Companies.AnyAsync(company=>company.Id==r.CompanyId))return NotFound();var code=r.Code.Trim();if(await db.Shops.AnyAsync(other=>other.Id!=id&&other.CompanyId==r.CompanyId&&other.Code==code))return Conflict(new{error="duplicate_shop",message="Shop code is already in use for this company."});if(x.CompanyId!=r.CompanyId)x.CreatedByRepId=null;x.CompanyId=r.CompanyId;x.Code=code;x.Name=r.Name.Trim();x.ContactName=r.ContactName.Trim();x.Phone=r.Phone.Trim();x.Address=r.Address.Trim();x.City=r.City.Trim();x.CreditLimit=r.CreditLimit;x.Active=r.Active;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("shops/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> DeleteShop(Guid id){
        if(await db.Orders.AnyAsync(x=>x.ShopId==id)||await db.Cheques.AnyAsync(x=>x.ShopId==id))return Conflict(new{message="Delete this shop's orders and checks first."});
        return await SoftDelete(db.Shops,id);
    }

    [HttpGet("products")]
    public async Task<object> Products([FromQuery]Guid? companyId,[FromQuery]string? search="",[FromQuery]bool? active=null,[FromQuery]int page=1,[FromQuery]int pageSize=25){var q=db.Products.AsNoTracking().AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.CompanyId==User.CompanyId());else if(companyId.HasValue)q=q.Where(x=>x.CompanyId==companyId);if(active.HasValue)q=q.Where(x=>x.Active==active);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Name.Contains(search)||x.Sku.Contains(search)||x.Barcode.Contains(search));return await Page(q.OrderBy(x=>x.Name),page,pageSize);}
    [HttpGet("products/stocks")]
    public async Task<object> ProductStocks(){var q=db.InventoryTransactions.AsNoTracking().AsQueryable();if(!User.IsAdmin())q=q.Where(x=>db.Products.Any(p=>p.Id==x.ProductId&&p.CompanyId==User.CompanyId()));return await q.GroupBy(x=>x.ProductId).Select(group=>new{productId=group.Key,currentStock=group.Sum(x=>x.QuantityIn-x.QuantityOut)}).ToListAsync();}
    [HttpPost("products"),Authorize(Roles="Admin")]
    public async Task<IActionResult> CreateProduct(ProductRequest r){var x=await operations.CreateProduct(r);return Created($"/api/products/{x.Id}",x);}
    [HttpPut("products/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> UpdateProduct(Guid id,ProductRequest r){var x=await db.Products.FindAsync(id);if(x is null)return NotFound();if(!await db.Companies.AnyAsync(company=>company.Id==r.CompanyId))return NotFound();if(r.SellingPrice<0||r.CostPrice<0||r.Mrp<0||r.ReorderLevel<0)return ValidationProblem("Prices and minimum stock cannot be negative.");var sku=r.Sku.Trim();var barcode=r.Barcode.Trim();if(await db.Products.AnyAsync(other=>other.Id!=id&&(other.Barcode==barcode||other.CompanyId==r.CompanyId&&other.Sku==sku)))return Conflict(new{error="duplicate_product",message="SKU or barcode already exists."});x.CompanyId=r.CompanyId;x.Sku=sku;x.Barcode=barcode;x.Name=r.Name.Trim();x.Category=r.Category.Trim();x.SellingPrice=r.SellingPrice;x.CostPrice=r.CostPrice;x.Mrp=r.Mrp??x.Mrp??r.SellingPrice;x.ReorderLevel=r.ReorderLevel;x.ExpiryDate=r.ExpiryDate;x.Active=r.Active;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("products/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> DeleteProduct(Guid id){
        if(await db.OrderProducts.AnyAsync(x=>x.ProductId==id)||await db.StockInProducts.AnyAsync(x=>x.ProductId==id))return Conflict(new{message="Delete the orders and Stock In invoices containing this product first."});
        return await SoftDelete(db.Products,id);
    }
    [HttpGet("products/{id:guid}/stock")]
    public async Task<object> Stock(Guid id)=>new{productId=id,currentStock=await inventory.GetCurrentStock(id)};
    [HttpGet("products/{id:guid}/stock-history")]
    public Task<List<InventoryTransaction>> StockHistory(Guid id)=>inventory.GetStockHistory(id);
    [HttpPost("products/{id:guid}/adjustments"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Adjust(Guid id,StockAdjustmentRequest r){
        if(!await db.Products.AnyAsync(x=>x.Id==id))return NotFound();
        var legacyPrefix=r.Notes?.Split('|',2)[0].Trim();
        var name=(r.Reason??(legacyPrefix?.StartsWith("Reason: ",StringComparison.OrdinalIgnoreCase)==true?legacyPrefix[8..]:"")).Trim();
        if(name.Length>80)return ValidationProblem("Adjustment reason must be at most 80 characters.");
        var x=await inventory.CreateStockAdjustment(id,r.Quantity,r.Direction,r.Notes??"",r.UnitCost,r.UnitPrice);
        if(name.Length>0&&!await db.StockAdjustmentReasons.AnyAsync(item=>item.Name==name))
            db.StockAdjustmentReasons.Add(new StockAdjustmentReason{Name=name});
        await db.SaveChangesAsync();return Ok(x);
    }
    [HttpPut("products/{id:guid}/stock-history/{movementId:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> UpdateStockMovement(Guid id,Guid movementId,UpdateStockMovementRequest r){
        if(string.IsNullOrWhiteSpace(r.Reason))return ValidationProblem("Reason is required.");
        var x=await inventory.UpdateManualMovement(id,movementId,r.Date,r.Quantity,r.Reason,r.UnitCost,r.UnitPrice,r.Mrp);
        await db.SaveChangesAsync();return Ok(x);
    }
    [HttpDelete("products/{id:guid}/stock-history/{movementId:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> DeleteStockMovement(Guid id,Guid movementId){await inventory.DeleteManualMovement(id,movementId);await db.SaveChangesAsync();return NoContent();}

    private async Task<IActionResult> SoftDelete<T>(DbSet<T> set,Guid id) where T:Entity{var x=await set.FindAsync(id);if(x is null)return NotFound();x.IsDeleted=true;x.DeletedAt=DateTimeOffset.UtcNow;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return NoContent();}
    private static async Task<object> Page<T>(IQueryable<T> query,int page,int pageSize){page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var total=await query.CountAsync();return new{items=await query.Skip((page-1)*pageSize).Take(pageSize).ToListAsync(),page,pageSize,total,totalPages=(int)Math.Ceiling(total/(double)pageSize)};}
}

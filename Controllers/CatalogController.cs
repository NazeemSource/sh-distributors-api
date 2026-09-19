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
    public async Task<IActionResult> CreateCompany(CompanyRequest r){if(string.IsNullOrWhiteSpace(r.Code)||string.IsNullOrWhiteSpace(r.Name))return ValidationProblem("Code and name are required.");if(await db.Companies.AnyAsync(x=>x.Code==r.Code))return Conflict(new{error="duplicate_company"});var x=new Company{Code=r.Code.Trim(),Name=r.Name.Trim(),ContactName=r.ContactName.Trim(),Phone=r.Phone.Trim(),Address=r.Address.Trim(),Active=r.Active};db.Add(x);await db.SaveChangesAsync();return Created($"/api/companies/{x.Id}",x);}
    [HttpPut("companies/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> UpdateCompany(Guid id,CompanyRequest r){var x=await db.Companies.FindAsync(id);if(x is null)return NotFound();x.Code=r.Code.Trim();x.Name=r.Name.Trim();x.ContactName=r.ContactName.Trim();x.Phone=r.Phone.Trim();x.Address=r.Address.Trim();x.Active=r.Active;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("companies/{id:guid}"),Authorize(Roles="Admin")]
    public Task<IActionResult> DeleteCompany(Guid id)=>SoftDelete(db.Companies,id);

    [HttpGet("shops")]
    public async Task<object> Shops([FromQuery]Guid? companyId,[FromQuery]string? search="",[FromQuery]int page=1,[FromQuery]int pageSize=25){var q=db.Shops.AsNoTracking().AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.CompanyId==User.CompanyId());else if(companyId.HasValue)q=q.Where(x=>x.CompanyId==companyId);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Name.Contains(search)||x.Code.Contains(search));return await Page(q.OrderBy(x=>x.Name),page,pageSize);}
    [HttpGet("shops/{id:guid}/outstanding")]
    public async Task<object> ShopOutstanding(Guid id)=>new{shopId=id,outstanding=await payments.GetShopOutstanding(id),orders=await payments.GetShopOutstandingOrders(id)};
    [HttpPost("shops"),Authorize(Roles="Admin")]
    public async Task<IActionResult> CreateShop(ShopRequest r){if(r.CreditLimit<0)return ValidationProblem("Credit limit cannot be negative.");if(!await db.Companies.AnyAsync(x=>x.Id==r.CompanyId))return NotFound();if(await db.Shops.AnyAsync(x=>x.CompanyId==r.CompanyId&&x.Code==r.Code))return Conflict(new{error="duplicate_shop"});var x=new Shop{CompanyId=r.CompanyId,Code=r.Code.Trim(),Name=r.Name.Trim(),ContactName=r.ContactName.Trim(),Phone=r.Phone.Trim(),Address=r.Address.Trim(),City=r.City.Trim(),CreditLimit=r.CreditLimit,Active=r.Active};db.Add(x);await db.SaveChangesAsync();return Created($"/api/shops/{x.Id}",x);}
    [HttpPut("shops/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> UpdateShop(Guid id,ShopRequest r){var x=await db.Shops.FindAsync(id);if(x is null)return NotFound();x.CompanyId=r.CompanyId;x.Code=r.Code.Trim();x.Name=r.Name.Trim();x.ContactName=r.ContactName.Trim();x.Phone=r.Phone.Trim();x.Address=r.Address.Trim();x.City=r.City.Trim();x.CreditLimit=r.CreditLimit;x.Active=r.Active;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("shops/{id:guid}"),Authorize(Roles="Admin")]
    public Task<IActionResult> DeleteShop(Guid id)=>SoftDelete(db.Shops,id);

    [HttpGet("products")]
    public async Task<object> Products([FromQuery]Guid? companyId,[FromQuery]string? search="",[FromQuery]bool? active=null,[FromQuery]int page=1,[FromQuery]int pageSize=25){var q=db.Products.AsNoTracking().AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.CompanyId==User.CompanyId());else if(companyId.HasValue)q=q.Where(x=>x.CompanyId==companyId);if(active.HasValue)q=q.Where(x=>x.Active==active);if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Name.Contains(search)||x.Sku.Contains(search)||x.Barcode.Contains(search));return await Page(q.OrderBy(x=>x.Name),page,pageSize);}
    [HttpPost("products"),Authorize(Roles="Admin")]
    public async Task<IActionResult> CreateProduct(ProductRequest r){var x=await operations.CreateProduct(r);return Created($"/api/products/{x.Id}",x);}
    [HttpPut("products/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> UpdateProduct(Guid id,ProductRequest r){var x=await db.Products.FindAsync(id);if(x is null)return NotFound();if(r.SellingPrice<0||r.CostPrice<0)return ValidationProblem("Prices cannot be negative.");x.CompanyId=r.CompanyId;x.Sku=r.Sku.Trim();x.Barcode=r.Barcode.Trim();x.Name=r.Name.Trim();x.Category=r.Category.Trim();x.SellingPrice=r.SellingPrice;x.CostPrice=r.CostPrice;x.ReorderLevel=r.ReorderLevel;x.ExpiryDate=r.ExpiryDate;x.Active=r.Active;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("products/{id:guid}"),Authorize(Roles="Admin")]
    public Task<IActionResult> DeleteProduct(Guid id)=>SoftDelete(db.Products,id);
    [HttpGet("products/{id:guid}/stock")]
    public async Task<object> Stock(Guid id)=>new{productId=id,currentStock=await inventory.GetCurrentStock(id)};
    [HttpGet("products/{id:guid}/stock-history")]
    public Task<List<InventoryTransaction>> StockHistory(Guid id)=>inventory.GetStockHistory(id);
    [HttpPost("products/{id:guid}/adjustments"),Authorize(Roles="Admin")]
    public async Task<IActionResult> Adjust(Guid id,StockAdjustmentRequest r){if(!await db.Products.AnyAsync(x=>x.Id==id))return NotFound();var x=await inventory.CreateStockAdjustment(id,r.Quantity,r.Direction,r.Notes);await db.SaveChangesAsync();return Ok(x);}

    private async Task<IActionResult> SoftDelete<T>(DbSet<T> set,Guid id) where T:Entity{var x=await set.FindAsync(id);if(x is null)return NotFound();x.IsDeleted=true;x.DeletedAt=DateTimeOffset.UtcNow;x.UpdatedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return NoContent();}
    private static async Task<object> Page<T>(IQueryable<T> query,int page,int pageSize){page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);var total=await query.CountAsync();return new{items=await query.Skip((page-1)*pageSize).Take(pageSize).ToListAsync(),page,pageSize,total,totalPages=(int)Math.Ceiling(total/(double)pageSize)};}
}

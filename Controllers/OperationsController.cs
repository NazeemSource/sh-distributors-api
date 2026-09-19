using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Extensions;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Controllers;

[ApiController,Route("api"),Authorize]
public sealed class OperationsController(AppDbContext db,OperationsService operations,PaymentService payments):ControllerBase
{
    [HttpPost("stock-ins"),Authorize(Roles="Admin")]
    public async Task<IActionResult>CreateStockIn(StockInRequest r){var x=await operations.CreateStockIn(r);return Created($"/api/stock-ins/{x.Id}",View(x));}
    [HttpGet("stock-ins"),Authorize(Roles="Admin")]
    public async Task<object>StockIns([FromQuery]Guid? companyId,[FromQuery]DateOnly? from,[FromQuery]DateOnly? to,[FromQuery]string? paymentStatus){var q=db.StockIns.AsNoTracking().Include(x=>x.Products).AsQueryable();if(companyId.HasValue)q=q.Where(x=>x.CompanyId==companyId);if(from.HasValue)q=q.Where(x=>x.StockInDate>=from);if(to.HasValue)q=q.Where(x=>x.StockInDate<=to);if(!string.IsNullOrWhiteSpace(paymentStatus))q=q.Where(x=>x.PaymentStatus==paymentStatus);return await q.OrderByDescending(x=>x.StockInDate).Take(100).Select(x=>View(x)).ToListAsync();}
    [HttpPost("stock-ins/{id:guid}/payments"),Authorize(Roles="Admin")]
    public async Task<IActionResult>StockInPayment(Guid id,PaymentRequest r){var x=await operations.AddStockInPayment(id,r);return Ok(PaymentView(x));}
    [HttpGet("companies/{id:guid}/outstanding"),Authorize(Roles="Admin")]
    public async Task<object>CompanyOutstanding(Guid id)=>new{companyId=id,outstanding=await payments.GetCompanyOutstanding(id),stockIns=await payments.GetCompanyOutstandingStockIns(id)};

    [HttpPost("orders")]
    public async Task<IActionResult>CreateOrder(OrderRequest r){if(!User.IsAdmin()&&r.SalesRepId!=User.UserId())return Forbid();var x=await operations.CreateOrder(r);return Created($"/api/orders/{x.Id}",View(x));}
    [HttpGet("orders")]
    public async Task<object>Orders([FromQuery]Guid? shopId,[FromQuery]Guid? salesRepId,[FromQuery]DateOnly? from,[FromQuery]DateOnly? to,[FromQuery]string? paymentStatus){var q=db.Orders.AsNoTracking().Include(x=>x.Products).AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.SalesRepId==User.UserId());else if(salesRepId.HasValue)q=q.Where(x=>x.SalesRepId==salesRepId);if(shopId.HasValue)q=q.Where(x=>x.ShopId==shopId);if(from.HasValue)q=q.Where(x=>x.OrderDate>=from);if(to.HasValue)q=q.Where(x=>x.OrderDate<=to);if(!string.IsNullOrWhiteSpace(paymentStatus))q=q.Where(x=>x.PaymentStatus==paymentStatus);return await q.OrderByDescending(x=>x.OrderDate).Take(100).Select(x=>View(x)).ToListAsync();}
    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult>Order(Guid id){var q=db.Orders.AsNoTracking().Include(x=>x.Products).AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.SalesRepId==User.UserId());return await q.SingleOrDefaultAsync(x=>x.Id==id)is{}x?Ok(View(x)):NotFound();}
    [HttpPost("orders/{id:guid}/payments")]
    public async Task<IActionResult>OrderPayment(Guid id,PaymentRequest r){if(!User.IsAdmin()&&!await db.Orders.AnyAsync(x=>x.Id==id&&x.SalesRepId==User.UserId()))return NotFound();var x=await operations.AddOrderPayment(id,r);return Ok(PaymentView(x));}

    private static object View(StockIn x)=>new{x.Id,x.CompanyId,x.StockInNumber,x.StockInDate,x.StockTotal,x.PaymentStatus,x.Notes,Products=x.Products.Select(p=>new{p.Id,p.ProductId,p.Quantity,p.UnitCost,p.LineTotal})};
    private static object View(Order x)=>new{x.Id,x.CompanyId,x.ShopId,x.SalesRepId,x.OrderNumber,x.OrderDate,x.DeliveryDate,x.OrderTotal,x.PaymentStatus,x.Status,x.DeliveryAddress,x.Notes,Products=x.Products.Select(p=>new{p.Id,p.ProductId,p.Quantity,p.FreeIssueQuantity,p.UnitPrice,p.LineSubtotal})};
    private static object PaymentView(OrderPayment x)=>new{x.Id,x.OrderId,x.PaymentDate,x.PaidAmount,x.Method,x.Reference,x.CreatedAt};
    private static object PaymentView(StockInPayment x)=>new{x.Id,x.StockInId,x.PaymentDate,x.PaidAmount,x.Method,x.Reference,x.CreatedAt};
}

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
    [HttpDelete("stock-ins/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> DeleteStockIn(Guid id){await operations.DeleteStockIn(id);return NoContent();}
    [HttpDelete("stock-ins/{id:guid}/payments/{paymentId:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult> DeleteStockInPayment(Guid id,Guid paymentId){await operations.DeleteStockInPayment(id,paymentId);return NoContent();}
    [HttpPost("stock-ins"),Authorize(Roles="Admin")]
    public async Task<IActionResult>CreateStockIn(StockInRequest r){var x=await operations.CreateStockIn(r);return Created($"/api/stock-ins/{x.Id}",View(x));}
    [HttpGet("stock-ins"),Authorize(Roles="Admin")]
    public async Task<object>StockIns([FromQuery]Guid? companyId,[FromQuery]DateOnly? from,[FromQuery]DateOnly? to,[FromQuery]string? paymentStatus){var q=db.StockIns.AsNoTracking().Include(x=>x.Products).Include(x=>x.Payments).AsQueryable();if(companyId.HasValue)q=q.Where(x=>x.CompanyId==companyId);if(from.HasValue)q=q.Where(x=>x.StockInDate>=from);if(to.HasValue)q=q.Where(x=>x.StockInDate<=to);if(!string.IsNullOrWhiteSpace(paymentStatus))q=q.Where(x=>x.PaymentStatus==paymentStatus);return await q.OrderByDescending(x=>x.StockInDate).ThenByDescending(x=>x.CreatedAt).Select(x=>View(x)).ToListAsync();}
    [HttpPost("stock-ins/{id:guid}/payments"),Authorize(Roles="Admin")]
    public async Task<IActionResult>StockInPayment(Guid id,PaymentRequest r){var x=await operations.AddStockInPayment(id,r);return Ok(PaymentView(x));}
    [HttpGet("companies/{id:guid}/outstanding"),Authorize(Roles="Admin")]
    public async Task<object>CompanyOutstanding(Guid id)=>new{companyId=id,outstanding=await payments.GetCompanyOutstanding(id),stockIns=await payments.GetCompanyOutstandingStockIns(id)};

    [HttpPost("orders")]
    public async Task<IActionResult>CreateOrder(OrderRequest r){if(!User.IsAdmin()){if(r.SalesRepId!=User.UserId())return Forbid();if(!await db.Shops.AnyAsync(x=>x.Id==r.ShopId&&x.CompanyId==User.CompanyId()&&(x.CreatedByRepId==null||x.CreatedByRepId==User.UserId())))return NotFound();}var x=await operations.CreateOrder(r,!User.IsAdmin());return Created($"/api/orders/{x.Id}",View(x));}
    [HttpPost("orders/complete"),Authorize(Roles="Admin")]
    public async Task<IActionResult>CompleteOrders(CompleteOrdersRequest r){var completed=await operations.CompleteOrders(r);return Ok(completed.Select(View).ToList());}
    [HttpPut("orders/{id:guid}")]
    public async Task<IActionResult>UpdateOrder(Guid id,OrderRequest r){if(!User.IsAdmin()){var userId=User.UserId();if(r.SalesRepId!=userId||!await db.Orders.AnyAsync(x=>x.Id==id&&x.SalesRepId==userId))return Forbid();if(!await db.Shops.AnyAsync(x=>x.Id==r.ShopId&&x.CompanyId==User.CompanyId()&&(x.CreatedByRepId==null||x.CreatedByRepId==userId)))return NotFound();}var x=await operations.UpdateOrder(id,r);return Ok(View(x));}
    [HttpDelete("orders/{id:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult>DeleteOrder(Guid id){await operations.DeleteOrder(id);return NoContent();}
    [HttpGet("orders")]
    public async Task<object>Orders([FromQuery]Guid? shopId,[FromQuery]Guid? salesRepId,[FromQuery]DateOnly? from,[FromQuery]DateOnly? to,[FromQuery]string? paymentStatus){var q=db.Orders.AsNoTracking().Include(x=>x.Payments).Include(x=>x.Products).Include(x=>x.Returns).AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.SalesRepId==User.UserId());else if(salesRepId.HasValue)q=q.Where(x=>x.SalesRepId==salesRepId);if(shopId.HasValue)q=q.Where(x=>x.ShopId==shopId);if(from.HasValue)q=q.Where(x=>x.OrderDate>=from);if(to.HasValue)q=q.Where(x=>x.OrderDate<=to);if(!string.IsNullOrWhiteSpace(paymentStatus))q=q.Where(x=>x.PaymentStatus==paymentStatus);return await q.OrderByDescending(x=>x.OrderDate).Select(x=>View(x)).ToListAsync();}
    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult>Order(Guid id){var q=db.Orders.AsNoTracking().Include(x=>x.Payments).Include(x=>x.Products).Include(x=>x.Returns).AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.SalesRepId==User.UserId());return await q.SingleOrDefaultAsync(x=>x.Id==id)is{}x?Ok(View(x)):NotFound();}
    [HttpPost("orders/{id:guid}/payments")]
    public async Task<IActionResult>OrderPayment(Guid id,PaymentRequest r){if(!User.IsAdmin()&&!await db.Orders.AnyAsync(x=>x.Id==id&&x.SalesRepId==User.UserId()))return NotFound();var x=await operations.AddOrderPayment(id,r);return Ok(PaymentView(x));}
    [HttpDelete("orders/{id:guid}/payments/{paymentId:guid}"),Authorize(Roles="Admin")]
    public async Task<IActionResult>DeleteOrderPayment(Guid id,Guid paymentId){await operations.DeleteOrderPayment(id,paymentId);return NoContent();}
    [HttpGet("returns")]
    public async Task<object>Returns(){var q=db.ProductReturns.AsNoTracking().Include(x=>x.Order).AsQueryable();if(!User.IsAdmin())q=q.Where(x=>x.Order!.SalesRepId==User.UserId());return await q.OrderByDescending(x=>x.ReturnDate).ThenByDescending(x=>x.CreatedAt).Select(x=>ReturnView(x)).ToListAsync();}
    [HttpPost("orders/{id:guid}/returns")]
    public async Task<IActionResult>CreateReturn(Guid id,ProductReturnRequest r){if(!User.IsAdmin()&&!await db.Orders.AnyAsync(x=>x.Id==id&&x.SalesRepId==User.UserId()))return NotFound();var x=await operations.CreateProductReturn(id,r);await db.Entry(x).Reference(item=>item.Order).LoadAsync();return Created($"/api/returns/{x.Id}",ReturnView(x));}

    private static object View(StockIn x)=>new{x.Id,x.CompanyId,x.StockInNumber,x.StockInDate,x.StockTotal,x.PaymentStatus,x.Notes,Payments=x.Payments.Select(p=>PaymentView(p)),Products=x.Products.Select(p=>new{p.Id,p.ProductId,p.Quantity,p.UnitCost,p.LineTotal})};
    private static object View(Order x)=>new{x.Id,x.CompanyId,x.ShopId,x.SalesRepId,x.OrderNumber,x.OrderDate,x.DeliveryDate,x.OrderTotal,x.PaymentStatus,x.Status,x.DeliveryAddress,x.Notes,Payments=x.Payments.Select(p=>PaymentView(p)),Products=x.Products.Select(p=>new{p.Id,p.ProductId,p.Quantity,p.FreeIssueQuantity,p.DeliveredQuantity,p.StockCommittedQuantity,p.UnitPrice,p.Mrp,p.LineSubtotal}),Returns=x.Returns.Select(r=>new{r.Id,r.ReturnNumber,r.ProductId,r.ReturnDate,r.Quantity,r.UnitPrice,r.Amount,r.Condition,r.Reason})};
    private static object PaymentView(OrderPayment x)=>new{x.Id,x.OrderId,x.PaymentDate,x.PaidAmount,x.Method,x.Reference,x.CreatedAt};
    private static object PaymentView(StockInPayment x)=>new{x.Id,x.StockInId,x.PaymentDate,x.PaidAmount,x.Method,x.Reference,x.CreatedAt};
    private static object ReturnView(ProductReturn x)=>new{x.Id,x.ReturnNumber,x.OrderId,OrderNumber=x.Order!.OrderNumber,x.Order.CompanyId,x.Order.ShopId,x.Order.SalesRepId,x.ProductId,x.ReturnDate,x.Quantity,x.UnitPrice,x.Amount,x.Condition,x.Reason};
}

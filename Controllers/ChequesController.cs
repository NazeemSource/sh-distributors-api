using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Extensions;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Controllers;

[ApiController, Route("api/cheques"), Authorize]
public sealed class ChequesController(AppDbContext db) : ControllerBase
{
    private static readonly string[] Statuses = ["PENDING", "DEPOSITED", "CLEARED", "RETURNED", "CANCELLED"];

    [HttpGet]
    public async Task<object> Search([FromQuery] string? search, [FromQuery] Guid? shopId, [FromQuery] Guid? orderId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? status,
        [FromQuery] string sort = "date_desc", [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var query = db.Cheques.AsNoTracking().AsQueryable();
        if (!User.IsAdmin()) query = query.Where(x => x.Shop!.CompanyId == User.CompanyId());
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.ChequeNumber.Contains(search) || x.BankName.Contains(search));
        if (shopId.HasValue) query = query.Where(x => x.ShopId == shopId);
        if (orderId.HasValue) query = query.Where(x => x.OrderId == orderId);
        if (from.HasValue) query = query.Where(x => x.ChequeDate >= from);
        if (to.HasValue) query = query.Where(x => x.ChequeDate <= to);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.ToUpper());
        query = sort switch { "date_asc" => query.OrderBy(x => x.ChequeDate), "amount_asc" => query.OrderBy(x => x.Amount), "amount_desc" => query.OrderByDescending(x => x.Amount), _ => query.OrderByDescending(x => x.ChequeDate) };
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync();
        return new { items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => View(x)).ToListAsync(), page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) { var q=db.Cheques.AsNoTracking().AsQueryable(); if(!User.IsAdmin())q=q.Where(x=>x.Shop!.CompanyId==User.CompanyId()); return await q.SingleOrDefaultAsync(x => x.Id == id) is { } cheque ? Ok(View(cheque)) : NotFound(); }

    [HttpGet("upcoming")]
    public async Task<object> Upcoming([FromQuery] DateOnly? asOf)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query=db.Cheques.AsNoTracking().Where(x => x.Status == "PENDING" && x.ChequeDate >= today); if(!User.IsAdmin())query=query.Where(x=>x.Shop!.CompanyId==User.CompanyId()); var cheques = await query.OrderBy(x => x.ChequeDate).ToListAsync();
        return cheques.Where(x => x.ChequeDate <= today.AddDays(x.RemindBeforeDays)).Select(View);
    }

    [HttpGet("overdue")]
    public async Task<object> Overdue([FromQuery] DateOnly? asOf)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query=db.Cheques.AsNoTracking().Where(x => x.Status == "PENDING" && x.ChequeDate < today); if(!User.IsAdmin())query=query.Where(x=>x.Shop!.CompanyId==User.CompanyId()); return await query.OrderBy(x => x.ChequeDate).Select(x => View(x)).ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> Create(ChequeRequest request)
    {
        await Validate(request);
        await EnsureShopAccess(request.ShopId);
        if (await db.Cheques.AnyAsync(x => x.ChequeNumber == request.ChequeNumber.Trim())) throw new BusinessException("duplicate_cheque", "Cheque number already exists.", 409);
        var cheque = NewCheque(request); db.Cheques.Add(cheque); await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = cheque.Id }, View(cheque));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ChequeRequest request)
    {
        await Validate(request);
        await EnsureShopAccess(request.ShopId);
        var cheque = await db.Cheques.FindAsync(id) ?? throw new BusinessException("not_found", "Cheque was not found.", 404);
        if (await db.Cheques.AnyAsync(x => x.Id != id && x.ChequeNumber == request.ChequeNumber.Trim())) throw new BusinessException("duplicate_cheque", "Cheque number already exists.", 409);
        Apply(cheque, request); await db.SaveChangesAsync(); return Ok(View(cheque));
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var cheque = await db.Cheques.FindAsync(id) ?? throw new BusinessException("not_found", "Cheque was not found.", 404);
        cheque.IsDeleted = true; cheque.DeletedAt = DateTimeOffset.UtcNow; cheque.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); return NoContent();
    }

    private async Task Validate(ChequeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ChequeNumber) || string.IsNullOrWhiteSpace(request.BankName) || request.Amount <= 0 || request.RemindBeforeDays < 0)
            throw new BusinessException("validation_error", "Cheque number, bank, positive amount, and a non-negative reminder are required.");
        if (!Statuses.Contains(request.Status.ToUpper())) throw new BusinessException("invalid_status", "Cheque status is invalid.");
        var shop = await db.Shops.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ShopId) ?? throw new BusinessException("not_found", "Shop was not found.", 404);
        if (request.OrderId.HasValue)
        {
            var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OrderId) ?? throw new BusinessException("not_found", "Order was not found.", 404);
            if (order.ShopId != shop.Id) throw new BusinessException("invalid_order", "The order does not belong to this shop.");
        }
    }

    private async Task EnsureShopAccess(Guid shopId) { if(!User.IsAdmin()&&!await db.Shops.AnyAsync(x=>x.Id==shopId&&x.CompanyId==User.CompanyId())) throw new BusinessException("not_found","Shop was not found.",404); }

    private static Cheque NewCheque(ChequeRequest request) { var cheque = new Cheque { ShopId = request.ShopId, OrderId = request.OrderId, ChequeNumber = "", BankName = "", Amount = 0, ChequeDate = request.ChequeDate }; Apply(cheque, request); return cheque; }
    private static void Apply(Cheque cheque, ChequeRequest request) { cheque.ShopId = request.ShopId; cheque.OrderId = request.OrderId; cheque.ChequeNumber = request.ChequeNumber.Trim(); cheque.BankName = request.BankName.Trim(); cheque.Amount = request.Amount; cheque.ChequeDate = request.ChequeDate; cheque.Status = request.Status.ToUpper(); cheque.RemindBeforeDays = request.RemindBeforeDays; cheque.Notes = request.Notes.Trim(); cheque.UpdatedAt = DateTimeOffset.UtcNow; }
    private static object View(Cheque x) => new { x.Id, x.ShopId, x.OrderId, x.ChequeNumber, x.BankName, x.Amount, x.ChequeDate, x.Status, x.RemindBeforeDays, x.Notes, x.CreatedAt, x.UpdatedAt };
}

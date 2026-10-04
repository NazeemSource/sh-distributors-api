using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Controllers;

[ApiController, Route("api/deposit-accounts"), Authorize(Roles = "Admin")]
public sealed class DepositAccountsController(AppDbContext db) : ControllerBase
{
    private static readonly string[] Methods = ["Cash", "Bank transfer", "Check", "Card", "Other"];

    [HttpGet]
    public async Task<object> List()
    {
        var records = await db.DepositAccounts.AsNoTracking().Include(x => x.Payments)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt).ToListAsync();
        return records.Select(View).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) => await db.DepositAccounts.AsNoTracking()
        .Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id) is { } record
        ? Ok(View(record)) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CreateDepositAccountRequest request)
    {
        var type = request.Type.Trim();
        var amount = request.Amount ?? request.Total ?? 0;
        var dueDate = request.DueDate ?? request.Date;
        if (string.IsNullOrWhiteSpace(type)) return ValidationProblem("Type is required.");
        if (dueDate < request.Date) return ValidationProblem("Due date cannot be before the account date.");
        if (amount <= 0 || request.Paid < 0 || request.Paid > amount)
            return ValidationProblem("Amount must be positive and Paid cannot exceed Amount.");
        if (!ValidMoney(amount) || !ValidMoney(request.Paid)) return ValidationProblem("Amounts must have at most two decimal places.");
        if (request.Paid > 0 && !Methods.Contains(request.PaymentMethod))
            return ValidationProblem("Choose a payment method for the paid amount.");
        if (!await db.Companies.AnyAsync(x => x.Id == request.CompanyId)) return NotFound(new { message = "Company not found." });
        var record = new DepositAccount {
            CompanyId = request.CompanyId, Date = request.Date, DueDate = dueDate, Type = type,
            Account = string.IsNullOrWhiteSpace(request.Account) ? type : request.Account.Trim(), Total = amount
        };
        if (request.Paid > 0) record.Payments.Add(new DepositAccountPayment {
            PaymentDate = request.Date, Amount = request.Paid, Method = request.PaymentMethod
        });
        db.DepositAccounts.Add(record);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = record.Id }, View(record));
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddPayment(Guid id, AddDepositAccountPaymentRequest request)
    {
        var record = await db.DepositAccounts.Include(x => x.Payments).SingleOrDefaultAsync(x => x.Id == id);
        if (record is null) return NotFound();
        if (!Methods.Contains(request.Method)) return ValidationProblem("Choose a valid payment method.");
        if (request.PaymentDate < record.Date) return ValidationProblem("Payment date cannot be before the account date.");
        var due = record.Total - record.Payments.Sum(x => x.Amount);
        if (request.Amount <= 0 || request.Amount > due || !ValidMoney(request.Amount))
            return ValidationProblem("Payment must be positive, within the due amount, and have at most two decimal places.");
        var payment = new DepositAccountPayment {
            DepositAccountId = id, PaymentDate = request.PaymentDate,
            Amount = request.Amount, Method = request.Method, Reference = (request.Reference ?? "").Trim()
        };
        db.DepositAccountPayments.Add(payment);
        record.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(View(record));
    }

    private static bool ValidMoney(decimal amount) => decimal.Round(amount, 2) == amount;

    private static object View(DepositAccount record)
    {
        var payments = record.Payments.OrderBy(x => x.PaymentDate).ThenBy(x => x.CreatedAt).ToList();
        var paid = payments.Sum(x => x.Amount);
        var due = Math.Max(0, record.Total - paid);
        return new {
            record.Id, record.CompanyId, record.Date, DueDate = record.DueDate ?? record.Date, record.Type, record.Account, record.Total,
            Paid = paid, Due = due, PaymentMethod = payments.LastOrDefault()?.Method ?? "",
            Status = due == 0 ? "Paid" : paid > 0 ? "Partially paid" : "Pending",
            Payments = payments.Select(x => new { x.Id, x.PaymentDate, x.Amount, x.Method, x.Reference }).ToList()
        };
    }
}

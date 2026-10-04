using System.Text.Json;
using Distributor.Api.Contracts;
using Distributor.Api.Controllers;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class AccountEntryTests
{
    [Fact]
    public async Task Company_keeps_category_address_and_city_separately()
    {
        await using var db = CreateDb();
        var controller = new CatalogController(db, new OperationsService(db, new InventoryService(db), new PaymentService(db)), new InventoryService(db), new PaymentService(db));

        var result = await controller.CreateCompany(new CompanyRequest("SUP-1", "Supplier", "Contact", "0771234567", "12 Main Road", true, "Beverages", "Colombo"));

        Assert.IsType<CreatedResult>(result);
        var company = await db.Companies.SingleAsync();
        Assert.Equal("Beverages", company.Category);
        Assert.Equal("12 Main Road", company.Address);
        Assert.Equal("Colombo", company.City);
    }

    [Fact]
    public async Task Account_accepts_custom_type_amount_and_due_date_and_starts_unpaid()
    {
        await using var db = CreateDb();
        var company = new Company { Code = "ACC-CO", Name = "Account company" };
        db.Companies.Add(company); await db.SaveChangesAsync();
        var controller = new DepositAccountsController(db);
        var date = new DateOnly(2026, 10, 4);

        var result = await controller.Create(new CreateDepositAccountRequest(company.Id, date, "Invoice payment", Amount: 250, DueDate: date.AddDays(14)));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var json = JsonSerializer.SerializeToElement(created.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Invoice payment", json.GetProperty("type").GetString());
        Assert.Equal("Invoice payment", json.GetProperty("account").GetString());
        Assert.Equal(250, json.GetProperty("total").GetDecimal());
        Assert.Equal(0, json.GetProperty("paid").GetDecimal());
        Assert.Equal(250, json.GetProperty("due").GetDecimal());
        Assert.Equal("2026-10-18", json.GetProperty("dueDate").GetString());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }
}

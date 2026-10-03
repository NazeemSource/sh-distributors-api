using System.Security.Claims;
using System.Text.Json;
using Distributor.Api.Contracts;
using Distributor.Api.Controllers;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class ShopOwnershipTests
{
    [Fact]
    public async Task Rep_created_shops_are_visible_only_to_the_creator_and_admin()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var company = new Company { Code = "OWNERSHIP", Name = "Ownership company" };
        var otherCompany = new Company { Code = "OTHER", Name = "Other company" };
        var repA = new User { CompanyId = company.Id, Name = "Rep A", Username = "rep-a", PasswordHash = "hash", Role = "Rep" };
        var repB = new User { CompanyId = company.Id, Name = "Rep B", Username = "rep-b", PasswordHash = "hash", Role = "Rep" };
        var admin = new User { Name = "Admin", Username = "admin", PasswordHash = "hash", Role = "Admin" };
        db.AddRange(company, otherCompany, repA, repB, admin);
        await db.SaveChangesAsync();
        var inventory = new InventoryService(db);
        var payments = new PaymentService(db);
        var operations = new OperationsService(db, inventory, payments);
        CatalogController Catalog(User user) => new(db, operations, inventory, payments)
        {
            ControllerContext = Context(user)
        };

        var repRequest = new ShopRequest(company.Id, "CLIENT-CODE", "Rep A shop", "Owner", "0771234567", "Road", "Area", 1000);
        var repShop = Assert.IsType<Shop>(Assert.IsType<CreatedResult>(await Catalog(repA).CreateShop(repRequest)).Value);
        Assert.Equal(repA.Id, repShop.CreatedByRepId);
        Assert.NotEqual("CLIENT-CODE", repShop.Code);
        Assert.Equal(0, repShop.CreditLimit);
        var adminShop = Assert.IsType<Shop>(Assert.IsType<CreatedResult>(await Catalog(admin).CreateShop(
            new ShopRequest(company.Id, "ADMIN-CODE", "Shared shop", "Owner", "0771234567", "Road", "Area", 1000))).Value);
        Assert.Null(adminShop.CreatedByRepId);
        Assert.IsType<ForbidResult>(await Catalog(repA).CreateShop(repRequest with { CompanyId = otherCompany.Id }));

        async Task<Guid[]> VisibleShops(User user)
        {
            var response = JsonSerializer.SerializeToElement(await Catalog(user).Shops(null));
            return response.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("Id").GetGuid()).ToArray();
        }
        Assert.Contains(repShop.Id, await VisibleShops(repA));
        Assert.Contains(adminShop.Id, await VisibleShops(repA));
        Assert.DoesNotContain(repShop.Id, await VisibleShops(repB));
        Assert.Contains(adminShop.Id, await VisibleShops(repB));
        Assert.Contains(repShop.Id, await VisibleShops(admin));
        Assert.IsType<NotFoundResult>(await Catalog(repB).ShopOutstanding(repShop.Id));
        Assert.IsType<OkObjectResult>(await Catalog(repA).ShopOutstanding(repShop.Id));

        var privateCheque = new Cheque { Shop = repShop, ShopId = repShop.Id, ChequeNumber = "PRIVATE-CHEQUE", BankName = "Bank", Amount = 100, ChequeDate = new DateOnly(2026, 10, 3) };
        db.Cheques.Add(privateCheque);
        await db.SaveChangesAsync();
        var otherRepCheques = new ChequesController(db) { ControllerContext = Context(repB) };
        var adminCheques = new ChequesController(db) { ControllerContext = Context(admin) };
        Assert.Empty(JsonSerializer.SerializeToElement(await otherRepCheques.Search(null, null, null, null, null, null)).GetProperty("items").EnumerateArray());
        Assert.IsType<NotFoundResult>(await otherRepCheques.Get(privateCheque.Id));
        Assert.Single(JsonSerializer.SerializeToElement(await adminCheques.Search(null, null, null, null, null, null)).GetProperty("items").EnumerateArray());

        var snapshot = (JsonElement)await new OfflineData(db, inventory, payments).Snapshot(Principal(repB));
        var offlineShops = snapshot.GetProperty("reads").GetProperty("/api/shops").GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.DoesNotContain(repShop.Id, offlineShops);
        Assert.Contains(adminShop.Id, offlineShops);
        Assert.False(snapshot.GetProperty("reads").TryGetProperty($"/api/shops/{repShop.Id}/outstanding", out _));

        var otherRepOrders = new OperationsController(db, operations, payments) { ControllerContext = Context(repB) };
        Assert.IsType<NotFoundResult>(await otherRepOrders.CreateOrder(new OrderRequest(repShop.Id, repB.Id, "PRIVATE-ORDER", new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 3), "Road", "", [])));
    }

    private static ControllerContext Context(User user) => new()
    {
        HttpContext = new DefaultHttpContext { User = Principal(user) }
    };

    private static ClaimsPrincipal Principal(User user) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, user.Role),
         new Claim("company_id", user.CompanyId?.ToString() ?? "")], "test"));
}

using Distributor.Api.Domain;
using Distributor.Api.Services;
using Distributor.Api.Controllers;
using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class UserProfileTests
{
    [Fact]
    public void User_view_includes_sales_rep_profile_fields()
    {
        var user = new User
        {
            Name = "Demo Rep", Username = "demo_rep", PasswordHash = "hash", Role = "Rep",
            Territory = "Colombo", Address = "12 Main Street", Nic = "991234567V",
            Phone = "0771234567", Email = "demo@example.com", MonthlyTarget = 125000
        };

        var view = UserView.From(user);

        Assert.Equal(user.Address, view.Address);
        Assert.Equal(user.Nic, view.Nic);
        Assert.Equal(user.Phone, view.Phone);
        Assert.Equal(user.Email, view.Email);
        Assert.Equal(user.MonthlyTarget, view.MonthlyTarget);
    }

    [Fact]
    public async Task Rep_creation_accepts_missing_territory_and_target()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = new Company { Code = "KIST", Name = "KIST" };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var controller = new UsersController(db, new PasswordHasher<User>());

        var result = await controller.Create(new CreateUserRequest(company.Id, "New Rep", "new_rep", "Secure123", "Rep"));

        Assert.IsType<CreatedAtActionResult>(result);
        var rep = await db.Users.SingleAsync();
        Assert.Equal("", rep.Territory);
        Assert.Equal(0, rep.MonthlyTarget);
    }
}

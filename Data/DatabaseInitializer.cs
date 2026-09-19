using Distributor.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Data;

public sealed class DatabaseInitializer(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration configuration, IHostEnvironment environment)
{
    public async Task InitializeAsync()
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        if (!environment.IsDevelopment())
        {
            if (!configuration.GetValue<bool>("Seed:Enabled")) return;
            var password = configuration["Seed:AdminPassword"] ?? throw new InvalidOperationException("Seed:AdminPassword is required when production seeding is enabled.");
            if (password.Length < 12) throw new InvalidOperationException("The initial production admin password must contain at least 12 characters.");
            db.Users.Add(NewUser(configuration["Seed:AdminName"] ?? "Administrator - 01", configuration["Seed:AdminUsername"] ?? "admin", "Admin", null, "All areas", password));
            await db.SaveChangesAsync();
            return;
        }

        var users = new[]
        {
            NewUser("Administrator - 01", "admin", "Admin", null, "All areas", "1234"),
            NewUser("Rep - 01", "rep01", "Rep", "c1", "Area - 01", "1234"),
            NewUser("Rep - 02", "rep02", "Rep", "c1", "Area - 02", "1234"),
            NewUser("Rep - 03", "rep03", "Rep", "c2", "Area - 03", "1234")
        };
        await db.Users.AddRangeAsync(users);
        await db.SaveChangesAsync();
    }

    private User NewUser(string name, string username, string role, string? companyId, string territory, string password)
    {
        var user = new User { Name = name, Username = username, PasswordHash = "", Role = role, CompanyId = companyId, Territory = territory };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }
}

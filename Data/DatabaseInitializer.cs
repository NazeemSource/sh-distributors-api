using Distributor.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Distributor.Api.Data;

public sealed class DatabaseInitializer(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration configuration, IHostEnvironment environment)
{
    public async Task InitializeAsync()
    {
        if (db.Database.IsRelational() && !configuration.GetValue<bool>("Database:UseEnsureCreated"))
            await db.Database.MigrateAsync();
        else
        {
            await db.Database.EnsureCreatedAsync();
            if (db.Database.IsRelational()) await UpgradeLegacySchemaAsync();
        }
        if (await db.Users.AnyAsync()) return;

        if (!environment.IsDevelopment())
        {
            if (!configuration.GetValue<bool>("Seed:Enabled")) return;
            var password = configuration["Seed:AdminPassword"] ?? throw new InvalidOperationException("Seed:AdminPassword is required when production seeding is enabled.");
            if (password.Length < 6) throw new InvalidOperationException("The initial production admin password must contain at least 6 characters.");
            db.Users.Add(NewUser(configuration["Seed:AdminName"] ?? "Administrator - 01", configuration["Seed:AdminUsername"] ?? "admin", "Admin", null, "All areas", password));
            await db.SaveChangesAsync();
            return;
        }

        var company1 = new Company { Code = "COMPANY-01", Name = "Company - 01" };
        var company2 = new Company { Code = "COMPANY-02", Name = "Company - 02" };
        db.Companies.AddRange(company1, company2);
        var users = new[]
        {
            NewUser("Administrator - 01", "admin", "Admin", null, "All areas", "1234"),
            NewUser("Rep - 01", "rep01", "Rep", company1.Id, "Area - 01", "1234"),
            NewUser("Rep - 02", "rep02", "Rep", company1.Id, "Area - 02", "1234"),
            NewUser("Rep - 03", "rep03", "Rep", company2.Id, "Area - 03", "1234")
        };
        await db.Users.AddRangeAsync(users);
        await db.SaveChangesAsync();
    }

    private async Task UpgradeLegacySchemaAsync()
    {
        // Production began with EnsureCreated, so EF migrations cannot be applied to
        // that existing database. Add only the missing column, keeping existing data.
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'OrderProducts' AND COLUMN_NAME = 'DeliveredQuantity'";
            var exists = Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
            if (!exists)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `OrderProducts` ADD COLUMN `DeliveredQuantity` decimal(18,2) NOT NULL DEFAULT 0");
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private User NewUser(string name, string username, string role, Guid? companyId, string territory, string password)
    {
        var user = new User { Name = name, Username = username, PasswordHash = "", Role = role, CompanyId = companyId, Territory = territory };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }
}

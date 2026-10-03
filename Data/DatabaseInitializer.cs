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
        if (db.Database.IsRelational()) await db.Database.ExecuteSqlRawAsync(Distributor.Api.Services.OfflineSyncMiddleware.Schema);
        await BackfillAdjustmentReasonsAsync();
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
        // that existing database. Apply additive, idempotent upgrades while keeping data.
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await AddColumnIfMissingAsync(connection, "OrderProducts", "DeliveredQuantity", "decimal(18,2) NOT NULL DEFAULT 0");
            await AddColumnIfMissingAsync(connection, "Users", "Address", "varchar(300) NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, "Users", "Nic", "varchar(40) NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, "Users", "Phone", "varchar(40) NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, "Users", "Email", "varchar(180) NOT NULL DEFAULT ''");
            await AddColumnIfMissingAsync(connection, "Users", "MonthlyTarget", "decimal(18,2) NOT NULL DEFAULT 0");
            await MakeStockInReferenceOptionalAsync(connection);
            await AddColumnIfMissingAsync(connection, "Shops", "CreatedByRepId", "char(36) NULL");
            await AddShopOwnerIndexIfMissingAsync(connection);
            await AddShopOwnerForeignKeyIfMissingAsync(connection);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS `BrandingSettings` (
                  `Id` char(36) NOT NULL,
                  `Name` varchar(120) NOT NULL,
                  `LogoDataUrl` longtext NOT NULL,
                  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
                  `DeletedAt` datetime(6) NULL,
                  `CreatedAt` datetime(6) NOT NULL,
                  `UpdatedAt` datetime(6) NOT NULL,
                  CONSTRAINT `PK_BrandingSettings` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS `StockAdjustmentReasons` (
                  `Id` char(36) NOT NULL,
                  `Name` varchar(80) NOT NULL,
                  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
                  `DeletedAt` datetime(6) NULL,
                  `CreatedAt` datetime(6) NOT NULL,
                  `UpdatedAt` datetime(6) NOT NULL,
                  CONSTRAINT `PK_StockAdjustmentReasons` PRIMARY KEY (`Id`),
                  UNIQUE KEY `IX_StockAdjustmentReasons_Name` (`Name`)
                ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS `DepositAccounts` (
                  `Id` char(36) NOT NULL,
                  `CompanyId` char(36) NOT NULL,
                  `Date` date NOT NULL,
                  `Type` varchar(30) NOT NULL,
                  `Account` varchar(160) NOT NULL,
                  `Total` decimal(18,2) NOT NULL,
                  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
                  `DeletedAt` datetime(6) NULL,
                  `CreatedAt` datetime(6) NOT NULL,
                  `UpdatedAt` datetime(6) NOT NULL,
                  CONSTRAINT `PK_DepositAccounts` PRIMARY KEY (`Id`),
                  CONSTRAINT `FK_DepositAccounts_Companies_CompanyId` FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE RESTRICT,
                  KEY `IX_DepositAccounts_CompanyId_Date` (`CompanyId`, `Date`)
                ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_general_ci;
                """);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS `DepositAccountPayments` (
                  `Id` char(36) NOT NULL,
                  `DepositAccountId` char(36) NOT NULL,
                  `PaymentDate` date NOT NULL,
                  `Amount` decimal(18,2) NOT NULL,
                  `Method` varchar(40) NOT NULL,
                  `Reference` varchar(120) NOT NULL,
                  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
                  `DeletedAt` datetime(6) NULL,
                  `CreatedAt` datetime(6) NOT NULL,
                  `UpdatedAt` datetime(6) NOT NULL,
                  CONSTRAINT `PK_DepositAccountPayments` PRIMARY KEY (`Id`),
                  CONSTRAINT `FK_DepositAccountPayments_DepositAccounts_DepositAccountId` FOREIGN KEY (`DepositAccountId`) REFERENCES `DepositAccounts` (`Id`) ON DELETE RESTRICT,
                  KEY `IX_DepositAccountPayments_DepositAccountId` (`DepositAccountId`)
                ) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_general_ci;
                """);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private async Task BackfillAdjustmentReasonsAsync()
    {
        var known = new HashSet<string>(await db.StockAdjustmentReasons.Select(x => x.Name).ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var notes = await db.InventoryTransactions.AsNoTracking().Where(x => x.Type == "ADJUSTMENT" && x.Notes.StartsWith("Reason: ")).Select(x => x.Notes).ToListAsync();
        foreach (var note in notes)
        {
            var separator = note.IndexOf(" | Date:", StringComparison.Ordinal);
            var name = (separator >= 0 ? note[8..separator] : note[8..].Split('|', 2)[0]).Trim();
            if (name.Length is >0 and <=80 && known.Add(name)) db.StockAdjustmentReasons.Add(new StockAdjustmentReason { Name = name });
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
    }

    private static async Task AddColumnIfMissingAsync(System.Data.Common.DbConnection connection, string table, string column, string definition)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column";
        var tableParameter = check.CreateParameter(); tableParameter.ParameterName = "@table"; tableParameter.Value = table; check.Parameters.Add(tableParameter);
        var columnParameter = check.CreateParameter(); columnParameter.ParameterName = "@column"; columnParameter.Value = column; check.Parameters.Add(columnParameter);
        if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}";
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task MakeStockInReferenceOptionalAsync(System.Data.Common.DbConnection connection)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockIns' AND COLUMN_NAME = 'StockInNumber'";
        if (string.Equals((string?)await check.ExecuteScalarAsync(), "YES", StringComparison.OrdinalIgnoreCase)) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE `StockIns` MODIFY COLUMN `StockInNumber` varchar(255) NULL";
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task AddShopOwnerIndexIfMissingAsync(System.Data.Common.DbConnection connection)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Shops' AND INDEX_NAME = 'IX_Shops_CreatedByRepId'";
        if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = "CREATE INDEX `IX_Shops_CreatedByRepId` ON `Shops` (`CreatedByRepId`)";
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task AddShopOwnerForeignKeyIfMissingAsync(System.Data.Common.DbConnection connection)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Shops' AND CONSTRAINT_NAME = 'FK_Shops_Users_CreatedByRepId'";
        if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE `Shops` ADD CONSTRAINT `FK_Shops_Users_CreatedByRepId` FOREIGN KEY (`CreatedByRepId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT";
        await alter.ExecuteNonQueryAsync();
    }

    private User NewUser(string name, string username, string role, Guid? companyId, string territory, string password)
    {
        var user = new User { Name = name, Username = username, PasswordHash = "", Role = role, CompanyId = companyId, Territory = territory };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }
}

using Distributor.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<StockIn> StockIns => Set<StockIn>();
    public DbSet<StockInProduct> StockInProducts => Set<StockInProduct>();
    public DbSet<StockInPayment> StockInPayments => Set<StockInPayment>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderProduct> OrderProducts => Set<OrderProduct>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<Cheque> Cheques => Set<Cheque>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("utf8mb4_unicode_ci");
        var user = modelBuilder.Entity<User>();
        user.ToTable("Users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Id).HasColumnType("char(36)");
        user.HasIndex(x => x.Username).IsUnique();
        user.Property(x => x.Name).HasMaxLength(120).IsRequired();
        user.Property(x => x.Username).HasMaxLength(80).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        user.Property(x => x.Role).HasMaxLength(30).IsRequired();
        user.Property(x => x.CompanyId).HasColumnType("char(36)");
        user.Property(x => x.Territory).HasMaxLength(120);
        user.HasQueryFilter(x => !x.IsDeleted);
        user.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);

        ConfigureEntity<Company>(modelBuilder, "Companies");
        ConfigureEntity<Shop>(modelBuilder, "Shops");
        ConfigureEntity<Product>(modelBuilder, "Products");
        ConfigureEntity<InventoryTransaction>(modelBuilder, "InventoryTransactions");
        ConfigureEntity<StockIn>(modelBuilder, "StockIns");
        ConfigureEntity<StockInProduct>(modelBuilder, "StockInProducts");
        ConfigureEntity<StockInPayment>(modelBuilder, "StockInPayments");
        ConfigureEntity<Order>(modelBuilder, "Orders");
        ConfigureEntity<OrderProduct>(modelBuilder, "OrderProducts");
        ConfigureEntity<OrderPayment>(modelBuilder, "OrderPayments");
        ConfigureEntity<Cheque>(modelBuilder, "Cheques");

        modelBuilder.Entity<Company>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Company>().HasIndex(x => x.Name);
        modelBuilder.Entity<Shop>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        modelBuilder.Entity<Shop>().HasIndex(x => x.Name);
        modelBuilder.Entity<Shop>().HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Product>().HasIndex(x => new { x.CompanyId, x.Sku }).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Barcode).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Name);
        modelBuilder.Entity<Product>().HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InventoryTransaction>().HasIndex(x => new { x.ProductId, x.TransactionDate });
        modelBuilder.Entity<InventoryTransaction>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockIn>().HasIndex(x => x.StockInNumber).IsUnique();
        modelBuilder.Entity<StockIn>().HasIndex(x => new { x.CompanyId, x.StockInDate });
        modelBuilder.Entity<StockIn>().HasIndex(x => x.PaymentStatus);
        modelBuilder.Entity<StockIn>().HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockInProduct>().HasIndex(x => new { x.StockInId, x.ProductId }).IsUnique();
        modelBuilder.Entity<StockInProduct>().HasOne(x => x.StockIn).WithMany(x => x.Products).HasForeignKey(x => x.StockInId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockInProduct>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockInPayment>().HasOne(x => x.StockIn).WithMany(x => x.Payments).HasForeignKey(x => x.StockInId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Order>().HasIndex(x => x.OrderNumber).IsUnique();
        modelBuilder.Entity<Order>().HasIndex(x => new { x.ShopId, x.OrderDate });
        modelBuilder.Entity<Order>().HasIndex(x => new { x.SalesRepId, x.OrderDate });
        modelBuilder.Entity<Order>().HasIndex(x => x.PaymentStatus);
        modelBuilder.Entity<Order>().HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Order>().HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Order>().HasOne(x => x.SalesRep).WithMany().HasForeignKey(x => x.SalesRepId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrderProduct>().HasIndex(x => new { x.OrderId, x.ProductId }).IsUnique();
        modelBuilder.Entity<OrderProduct>().HasOne(x => x.Order).WithMany(x => x.Products).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrderProduct>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OrderPayment>().HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Cheque>().HasIndex(x => x.ChequeNumber).IsUnique();
        modelBuilder.Entity<Cheque>().HasIndex(x => new { x.ShopId, x.ChequeDate });
        modelBuilder.Entity<Cheque>().HasIndex(x => new { x.Status, x.ChequeDate });
        modelBuilder.Entity<Cheque>().HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Cheque>().HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()).Where(x => x.ClrType == typeof(decimal)))
            property.SetColumnType("decimal(18,2)");
    }

    private static void ConfigureEntity<T>(ModelBuilder modelBuilder, string table) where T : Entity
    {
        var entity = modelBuilder.Entity<T>();
        entity.ToTable(table);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("char(36)");
        entity.HasQueryFilter(x => !x.IsDeleted);
    }
}

using Distributor.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("Users");
        user.HasKey(x => x.Id);
        user.HasIndex(x => x.Username).IsUnique();
        user.Property(x => x.Name).HasMaxLength(120).IsRequired();
        user.Property(x => x.Username).HasMaxLength(80).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        user.Property(x => x.Role).HasMaxLength(30).IsRequired();
        user.Property(x => x.CompanyId).HasMaxLength(50);
        user.Property(x => x.Territory).HasMaxLength(120);
    }
}


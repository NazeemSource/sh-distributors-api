using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Distributor.Api.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL("Server=127.0.0.1;Port=3306;Database=distributor_api;User=design;Password=design")
            .Options;
        return new AppDbContext(options);
    }
}

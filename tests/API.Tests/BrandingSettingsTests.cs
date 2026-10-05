using Distributor.Api.Contracts;
using Distributor.Api.Controllers;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Tests;

public sealed class BrandingSettingsTests
{
    [Fact]
    public async Task Branding_defaults_to_sh_distributor_and_can_be_updated()
    {
        await using var db = CreateDb();
        var controller = new SettingsController(db);

        var initial = Assert.IsType<OkObjectResult>(await controller.Branding());
        Assert.Contains("SH Distributor", System.Text.Json.JsonSerializer.Serialize(initial.Value));

        var saved = Assert.IsType<OkObjectResult>(await controller.UpdateBranding(new BrandingSettingsRequest("New Distributor", "data:image/png;base64,AA==", "210303", "202/09 Lower Road", "Trincomalee", "0771234567", "Cargills Food & Beverage Limited")));
        Assert.Contains("New Distributor", System.Text.Json.JsonSerializer.Serialize(saved.Value));
        var stored = await db.BrandingSettings.SingleAsync();
        Assert.Equal("New Distributor", stored.Name);
        Assert.Equal("210303", stored.Code);
        Assert.Equal("202/09 Lower Road", stored.Address);
        Assert.Equal("Trincomalee", stored.City);
        Assert.Equal("0771234567", stored.Phone);
        Assert.Equal("Cargills Food & Beverage Limited", stored.AuthorizedDistributorOf);
        await controller.UpdateBranding(new BrandingSettingsRequest("Renamed Distributor", ""));
        Assert.Equal("210303", (await db.BrandingSettings.SingleAsync()).Code);
    }

    [Fact]
    public async Task Branding_rejects_unsafe_logo_content()
    {
        await using var db = CreateDb();
        var result = await new SettingsController(db).UpdateBranding(new BrandingSettingsRequest("SH Distributor", "data:image/svg+xml;base64,PHN2Zz4="));
        Assert.IsType<ObjectResult>(result);
        Assert.Empty(db.BrandingSettings);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

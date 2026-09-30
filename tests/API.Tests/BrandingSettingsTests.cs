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

        var saved = Assert.IsType<OkObjectResult>(await controller.UpdateBranding(new BrandingSettingsRequest("New Distributor", "data:image/png;base64,AA==")));
        Assert.Contains("New Distributor", System.Text.Json.JsonSerializer.Serialize(saved.Value));
        Assert.Equal("New Distributor", (await db.BrandingSettings.SingleAsync()).Name);
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

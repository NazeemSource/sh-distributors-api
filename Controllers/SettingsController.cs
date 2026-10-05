using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Controllers;

[ApiController, Route("api/settings")]
public sealed class SettingsController(AppDbContext db) : ControllerBase
{
    private const string DefaultName = "SH Distributor";

    [Authorize(Roles = "Admin"), HttpGet("adjustment-reasons")]
    public async Task<object> AdjustmentReasons() => await db.StockAdjustmentReasons.AsNoTracking()
        .OrderBy(x => x.Name).Select(x => x.Name).ToListAsync();

    [AllowAnonymous, HttpGet("branding")]
    public async Task<IActionResult> Branding()
    {
        var settings = await db.BrandingSettings.AsNoTracking().OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
        return Ok(new { name = settings?.Name ?? DefaultName, logoDataUrl = settings?.LogoDataUrl ?? "", code = settings?.Code ?? "", address = settings?.Address ?? "", city = settings?.City ?? "", phone = settings?.Phone ?? "", authorizedDistributorOf = settings?.AuthorizedDistributorOf ?? "" });
    }

    [Authorize(Roles = "Admin"), HttpPut("branding")]
    public async Task<IActionResult> UpdateBranding(BrandingSettingsRequest request)
    {
        var name = request.Name.Trim();
        var logo = request.LogoDataUrl.Trim();
        if (name.Length is < 2 or > 120) return ValidationProblem("Distributor name must contain 2 to 120 characters.");
        if (logo.Length > 1_500_000 || logo.Length > 0 && !IsSafeLogo(logo))
            return ValidationProblem("Logo must be a PNG, JPEG, or WebP image smaller than 1 MB.");
        var settings = await db.BrandingSettings.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new BrandingSetting { Name = name, LogoDataUrl = logo };
            db.BrandingSettings.Add(settings);
        }
        else
        {
            settings.Name = name;
            settings.LogoDataUrl = logo;
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }
        if (request.Code is not null) settings.Code = request.Code.Trim();
        if (request.Address is not null) settings.Address = request.Address.Trim();
        if (request.City is not null) settings.City = request.City.Trim();
        if (request.Phone is not null) settings.Phone = request.Phone.Trim();
        if (request.AuthorizedDistributorOf is not null) settings.AuthorizedDistributorOf = request.AuthorizedDistributorOf.Trim();
        await db.SaveChangesAsync();
        return Ok(new { name = settings.Name, logoDataUrl = settings.LogoDataUrl, code = settings.Code, address = settings.Address, city = settings.City, phone = settings.Phone, authorizedDistributorOf = settings.AuthorizedDistributorOf });
    }

    private static bool IsSafeLogo(string value) =>
        value.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase);
}

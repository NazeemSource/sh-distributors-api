using System.Security.Claims;

namespace Distributor.Api.Extensions;

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static Guid? CompanyId(this ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirstValue("company_id"), out var id) ? id : null;
    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole("Admin");
}

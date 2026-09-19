using System.Security.Claims;

namespace Distributor.Api.Extensions;

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}


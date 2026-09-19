using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Distributor.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Distributor.Api.Services;

public sealed class TokenService(IConfiguration configuration)
{
    public object Create(User user)
    {
        var expires = DateTime.UtcNow.AddHours(configuration.GetValue("Jwt:Hours", 12));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("display_name", user.Name)
        };
        if (user.CompanyId is not null) claims.Add(new("company_id", user.CompanyId.Value.ToString()));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"], audience: configuration["Jwt:Audience"], claims: claims,
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tokenType = "Bearer", expiresAt = expires, user = UserView.From(user) };
    }
}

public sealed record UserView(Guid Id, Guid? CompanyId, string Name, string Username, string Role, string Territory, bool Active, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static UserView From(User user) => new(user.Id, user.CompanyId, user.Name, user.Username, user.Role, user.Territory, user.Active, user.CreatedAt, user.UpdatedAt);
}

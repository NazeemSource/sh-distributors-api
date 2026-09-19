namespace Distributor.Api.Domain;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? CompanyId { get; set; }
    public required string Name { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public string Territory { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

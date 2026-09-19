namespace Distributor.Api.Contracts;

public sealed record LoginRequest(string Username, string Password);
public sealed record CreateUserRequest(string? CompanyId, string Name, string Username, string Password, string Role, string Territory, bool Active = true);
public sealed record UpdateUserRequest(string? CompanyId, string Name, string Role, string Territory, bool Active);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AdminResetPasswordRequest(string NewPassword);

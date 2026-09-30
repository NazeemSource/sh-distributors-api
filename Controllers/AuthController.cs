using Distributor.Api.Contracts;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Extensions;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distributor.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username && x.Active);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "invalid_credentials", message = "Username or password is incorrect." });
        return Ok(tokens.Create(user));
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me() => await db.Users.FindAsync(User.UserId()) is { } user ? Ok(UserView.From(user)) : NotFound();

    [Authorize(Roles = "Admin"), HttpPost("admin-credentials")]
    public async Task<IActionResult> UpdateAdminCredentials(UpdateAdminCredentialsRequest request)
    {
        var username = (request.Username ?? "").Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-z0-9_.-]{3,80}$"))
            return BadRequest(new { message = "Username must contain 3–80 letters, numbers, dots, underscores or hyphens." });
        if (!string.IsNullOrEmpty(request.NewPassword) && request.NewPassword.Length < 8)
            return BadRequest(new { message = "New password must contain at least 8 characters." });
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == User.UserId());
        if (user is null) return Unauthorized();
        if (string.IsNullOrEmpty(request.CurrentPassword) || hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect." });
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.Id != user.Id && x.Username == username))
            return Conflict(new { message = "Username is already in use." });
        user.Username = username;
        // Rehash even for a username-only change, invalidating previous sessions.
        user.PasswordHash = hasher.HashPassword(user, string.IsNullOrEmpty(request.NewPassword) ? request.CurrentPassword : request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (request.NewPassword.Length < 8) return ValidationProblem("New password must contain at least 8 characters.");
        var user = await db.Users.FindAsync(User.UserId());
        if (user is null) return NotFound();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { error = "invalid_password", message = "Current password is incorrect." });
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}


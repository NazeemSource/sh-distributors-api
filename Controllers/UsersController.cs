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

[ApiController, Route("api/users"), Authorize(Roles = "Admin")]
public sealed class UsersController(AppDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UserView>> List([FromQuery] string? role, [FromQuery] Guid? companyId, [FromQuery] bool includeInactive = false)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(x => x.Role == role);
        if (companyId.HasValue) query = query.Where(x => x.CompanyId == companyId);
        return await query.OrderBy(x => x.Name).Select(x => UserView.From(x)).ToListAsync();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) => await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) is { } user ? Ok(UserView.From(user)) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var validation = Validate(request.Name, request.Username, request.Password, request.Role, request.CompanyId);
        if (validation is not null) return ValidationProblem(validation);
        var username = request.Username.Trim().ToLowerInvariant();
        if (request.CompanyId.HasValue && !await db.Companies.AnyAsync(x => x.Id == request.CompanyId)) return NotFound(new { error = "company_not_found", message = "Company was not found." });
        if (await db.Users.AnyAsync(x => x.Username == username)) return Conflict(new { error = "username_exists", message = "Username is already in use." });
        var user = new User { Name = request.Name.Trim(), Username = username, PasswordHash = "", Role = NormalizeRole(request.Role), CompanyId = request.CompanyId, Territory = request.Territory.Trim(), Active = request.Active };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = user.Id }, UserView.From(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request)
    {
        var role = NormalizeRole(request.Role);
        if (string.IsNullOrWhiteSpace(request.Name) || role is not ("Admin" or "Rep") || role == "Rep" && !request.CompanyId.HasValue)
            return ValidationProblem("Name and a valid role are required; reps must have a company.");
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (request.CompanyId.HasValue && !await db.Companies.AnyAsync(x => x.Id == request.CompanyId)) return NotFound(new { error = "company_not_found", message = "Company was not found." });
        if (id == User.UserId() && !request.Active) return BadRequest(new { error = "cannot_deactivate_self", message = "You cannot deactivate your own account." });
        user.Name = request.Name.Trim(); user.Role = role; user.CompanyId = role == "Admin" ? null : request.CompanyId;
        user.Territory = request.Territory.Trim(); user.Active = request.Active; user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok(UserView.From(user));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, AdminResetPasswordRequest request)
    {
        if (request.NewPassword.Length < 8) return ValidationProblem("New password must contain at least 8 characters.");
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword); user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        if (id == User.UserId()) return BadRequest(new { error = "cannot_deactivate_self", message = "You cannot deactivate your own account." });
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.Active = false; user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(string name, string username, string password, string role, Guid? companyId)
    {
        var normalized = NormalizeRole(role);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username)) return "Name and username are required.";
        if (password.Length < 8) return "Password must contain at least 8 characters.";
        if (normalized is not ("Admin" or "Rep")) return "Role must be Admin or Rep.";
        if (normalized == "Rep" && !companyId.HasValue) return "A company is required for a sales rep.";
        return null;
    }
    private static string NormalizeRole(string role) => role.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : role.Equals("rep", StringComparison.OrdinalIgnoreCase) ? "Rep" : role.Trim();
}

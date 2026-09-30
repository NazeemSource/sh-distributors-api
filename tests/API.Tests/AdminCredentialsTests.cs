using System.Security.Claims;
using Distributor.Api.Contracts;
using Distributor.Api.Controllers;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace API.Tests;
public sealed class AdminCredentialsTests
{
    [Fact]
    public async Task Credentials_require_current_password_and_invalidate_old_session()
    {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hasher=new PasswordHasher<User>();var user=new User{Name="Admin",Username="admin",PasswordHash="",Role="Admin"};user.PasswordHash=hasher.HashPassword(user,"old-password");db.Add(user);await db.SaveChangesAsync();
        var version=TokenService.SecurityVersion(user);
        var controller=new AuthController(db,hasher,new TokenService(new ConfigurationBuilder().Build())){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Role,"Admin")],"test"))}}};
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateAdminCredentials(new("new-admin","wrong","new-password")));
        Assert.Equal("admin",user.Username);
        Assert.IsType<NoContentResult>(await controller.UpdateAdminCredentials(new("new-admin","old-password","new-password")));
        Assert.Equal("new-admin",user.Username);Assert.NotEqual(version,TokenService.SecurityVersion(user));
        Assert.Equal(PasswordVerificationResult.Failed,hasher.VerifyHashedPassword(user,user.PasswordHash,"old-password"));
        Assert.NotEqual(PasswordVerificationResult.Failed,hasher.VerifyHashedPassword(user,user.PasswordHash,"new-password"));
    }
}

using System.Text;
using Distributor.Api.Data;
using Distributor.Api.Domain;
using Distributor.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys")))
    .SetApplicationName("Distributor.Api");
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<OperationsService>();
builder.Services.AddScoped<OfflineData>();
builder.Services.AddSingleton<TokenService>();
if (builder.Configuration.GetValue<bool>("Database:UseInMemory"))
    builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("DistributorApi"));
else
{
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("The MySQL connection string is required.");
    builder.Services.AddDbContext<AppDbContext>(options => options.UseMySQL(connectionString));
}
builder.Services.AddCors(options => options.AddPolicy("Apps", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Offline-Versions")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromMinutes(1)
    };
    options.Events = new JwtBearerEvents {
        OnTokenValidated = async context => {
            var id=context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if(!Guid.TryParse(id,out var userId)){context.Fail("Invalid account.");return;}
            var db=context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==userId);
            if(user is null || !user.Active || context.Principal?.FindFirst("security_version")?.Value!=TokenService.SecurityVersion(user)
                || context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value!=user.Role
                || context.Principal?.FindFirst("company_id")?.Value!=user.CompanyId?.ToString())context.Fail("Account access has changed. Sign in again.");
        }
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("Apps");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OfflineSyncMiddleware>();
app.MapHealthChecks("/health");
app.MapControllers();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
app.Run();

public partial class Program;

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
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys")))
    .SetApplicationName("Distributor.Api");
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DatabaseInitializer>();
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
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("Apps");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
app.Run();

public partial class Program;

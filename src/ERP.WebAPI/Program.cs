using System;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Persistence.Contexts;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Services.AiEngine;
using ERP.Infrastructure.Services.Security;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Cache Support
builder.Services.AddMemoryCache();

// 2. Add DbContext with Dynamic Provider Support (SQLite for Dev/Verification, SQL Server for Prod)
var provider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var connString = builder.Configuration.GetConnectionString(provider);

if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connString, b => b.MigrationsAssembly("ERP.Infrastructure")));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connString, b => b.MigrationsAssembly("ERP.Infrastructure")));
}

// 3. Add Dependency Injection (Repositories, Unit of Work, NLP service, Token service)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<INlpService, NlpService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// 4. Add JWT Authentication Services
var secretKey = builder.Configuration["JwtSettings:SecretKey"] ?? "ThisIsAVerySecretKeyForERPDatabaseSystem2026!";
var issuer = builder.Configuration["JwtSettings:Issuer"] ?? "ErpSystem";
var audience = builder.Configuration["JwtSettings:Audience"] ?? "ErpSystemClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// 5. Add Rate Limiting Middleware
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("ChatPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                          ?? context.Connection.RemoteIpAddress?.ToString() 
                          ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// 6. Add Controllers
builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

var app = builder.Build();

// 7. Configure Middleware Pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration["DisableRateLimiting"] != "true")
{
    app.UseRateLimiter();
}

app.MapControllers();

app.Run();

// Expose public class Program so it can be referenced in WebAPI integration tests
public partial class Program { }

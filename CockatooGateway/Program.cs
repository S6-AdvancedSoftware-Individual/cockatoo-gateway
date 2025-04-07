using CockatooGateway;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configure app configuration
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:5001");

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer("Auth0", options =>
{
    options.Authority = $"https://{builder.Configuration["Auth0:Domain"]}/";
    options.Audience = builder.Configuration["Auth0:Audience"];
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = ClaimTypes.NameIdentifier
    };
});

builder.Services
  .AddAuthorization(options =>
  {
  });

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("scp");
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Add("scp", "scope");


builder.Services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();

// Add services
builder.Services.AddOcelot();


var app = builder.Build();

// Use CORS middleware
app.UseCors("AllowSpecificOrigins");

// Configure middleware
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseOcelot().Wait();

app.Run();

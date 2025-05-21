using CockatooGateway;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var betterstack_sourceToken = Environment.GetEnvironmentVariable("BETTERSTACK_SOURCETOKEN") ?? "";
var betterstack_endpoint = Environment.GetEnvironmentVariable("BETTERSTACK_ENDPOINT") ?? "";
var auth0_domain = Environment.GetEnvironmentVariable("AUTH0_DOMAIN") ?? "";
var auth0_audience = Environment.GetEnvironmentVariable("AUTH0_AUDIENCE") ?? "";

var builder = WebApplication.CreateBuilder(args);

//Configure SeriLog as the global logger.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.BetterStack(
        sourceToken: betterstack_sourceToken,
        betterStackEndpoint: betterstack_endpoint
    )
    .MinimumLevel.Information()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Host.UseSerilog();

// Configure app configuration
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

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

// Configure security and authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer("Auth0", options =>
{
    options.Authority = $"https://{auth0_domain}/";
    options.Audience = auth0_audience;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = ClaimTypes.NameIdentifier
    };
});

// Configure authorization policies
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("scp");
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Add("scp", "scope");

// Configure authorization & health checks
builder.Services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
builder.Services.AddHealthChecks();

// Add services
builder.Services.AddOcelot();

var app = builder.Build();

// Custom middleware to log unauthenticated requests
app.Use(async (context, next) =>
{
    if (!context.User.Identity?.IsAuthenticated ?? true)
    {
        Log.Warning("Unauthenticated request: {Method} at '{Path}' from '{IP}'",
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress);
    }
    await next();
});

// Use CORS middleware
app.UseCors("AllowSpecificOrigins");

// Configure the HTTP request pipeline
app.UseRouting();
app.UseAuthorization();

// Configure health checks
app.MapHealthChecks("/_health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Add health endpoint as controller
app.UseEndpoints(e =>
{
    e.MapControllers();
});


// Configure authentication and authorization
app.UseAuthentication();

app.MapControllers();

// Configure Ocelot middleware
app.UseOcelot().Wait();

app.Run();

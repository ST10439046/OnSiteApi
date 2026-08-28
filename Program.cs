using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnSiteApi.Data;
using OnSiteApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// 1. DATABASE
// =====================================================

// Get the PostgreSQL connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DefaultConnection was not found in appsettings.json.");
}

// Connect Entity Framework Core to Supabase PostgreSQL
builder.Services.AddDbContext<OnSiteDbContext>(options =>
    options.UseNpgsql(connectionString));


// =====================================================
// 2. SUPABASE JWT AUTHENTICATION
// =====================================================

var jwtSettings = builder.Configuration.GetSection("SupabaseJWT");

// Supabase project URL
var issuer = jwtSettings["Issuer"]
    ?? throw new InvalidOperationException(
        "SupabaseJWT:Issuer was not found in appsettings.json.");

// Supabase JWT audience
var audience = jwtSettings["Audience"]
    ?? throw new InvalidOperationException(
        "SupabaseJWT:Audience was not found in appsettings.json.");

// Supabase JWT signing keys
var jwksJson = jwtSettings["Jwks"]
    ?? throw new InvalidOperationException(
        "SupabaseJWT:Jwks was not found in appsettings.json.");

var jwks = new JsonWebKeySet(jwksJson);


// Configure JWT authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Check that the JWT came from your Supabase project
        ValidateIssuer = true,
        ValidIssuer = issuer,

        // Check that the JWT is intended for authenticated users
        ValidateAudience = true,
        ValidAudience = audience,

        // Reject expired JWTs
        ValidateLifetime = true,

        // Check the JWT signature
        ValidateIssuerSigningKey = true,
        IssuerSigningKeys = jwks.Keys
    };
});


// =====================================================
// 3. AUTHORIZATION
// =====================================================

builder.Services.AddAuthorization(options =>
{
    // Admin endpoints
    options.AddPolicy(
        "AdminOnly",
        policy => policy.RequireClaim("role", "admin"));

    // Foreman endpoints
    options.AddPolicy(
        "ForemanOnly",
        policy => policy.RequireClaim("role", "foreman"));

    // Truck driver endpoints
    options.AddPolicy(
        "DriverOnly",
        policy => policy.RequireClaim("role", "truck_driver"));
});


// =====================================================
// 4. BUILD APPLICATION
// =====================================================

var app = builder.Build();


// =====================================================
// 5. MIDDLEWARE
// =====================================================

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();


// =====================================================
// 6. API ENDPOINTS
// =====================================================

app.MapProfilesEndpoints();
app.MapSitesEndpoints();
app.MapAssignmentsEndpoints();
app.MapSiteUpdatesEndpoints();
app.MapTruckLogsEndpoints();


// =====================================================
// 7. ROOT / STATUS ENDPOINT
// =====================================================

app.MapGet("/", () => Results.Ok(new
{
    status = "On Site API is running.",
    framework = ".NET 9"
}));


// =====================================================
// 8. RUN APPLICATION
// =====================================================

app.Run();


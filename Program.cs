
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnSiteApi.Data;
using OnSiteApi.Endpoints;
using OnSiteApi.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DefaultConnection was not found.");
}

builder.Services.AddDbContext<OnSiteDbContext>(
    options =>
        options.UseNpgsql(connectionString));

builder.Services.AddSingleton<SupabaseAuthService>();

// =====================================================
// JSON SERIALIZATION
// =====================================================
// Serialize enums such as UserRole as strings instead
// of numeric values.
//
// Example:
// "role": "Foreman"
// instead of:
// "role": 1
// =====================================================

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

// =====================================================
// SUPABASE JWT CONFIGURATION
// =====================================================

var jwtSettings =
    builder.Configuration.GetSection(
        "SupabaseJWT");

var issuer =
    jwtSettings["Issuer"]
    ?? throw new InvalidOperationException(
        "SupabaseJWT:Issuer was not found.");

var audience =
    jwtSettings["Audience"]
    ?? throw new InvalidOperationException(
        "SupabaseJWT:Audience was not found.");

var jwksJson =
    jwtSettings["Jwks"]
    ?? throw new InvalidOperationException(
        "SupabaseJWT:Jwks was not found.");

var jwks =
    new JsonWebKeySet(jwksJson);

// =====================================================
// AUTHENTICATION
// =====================================================

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jwks.Keys
            };
    });

// =====================================================
// AUTHORIZATION POLICIES
// =====================================================

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "AdminOnly",
        policy =>
            policy.RequireClaim(
                "role",
                "admin"));

    options.AddPolicy(
        "ForemanOnly",
        policy =>
            policy.RequireClaim(
                "role",
                "foreman"));

    options.AddPolicy(
        "DriverOnly",
        policy =>
            policy.RequireClaim(
                "role",
                "truck_driver"));
});

var app = builder.Build();

// =====================================================
// HTTP PIPELINE
// =====================================================

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// =====================================================
// API ENDPOINTS
// =====================================================

app.MapProfilesEndpoints();
app.MapSitesEndpoints();
app.MapAssignmentsEndpoints();
app.MapSiteUpdatesEndpoints();
app.MapTruckLogsEndpoints();

// =====================================================
// API STATUS
// =====================================================

app.MapGet("/", () => Results.Ok(new
{
    status = "On Site API is running.",
    framework = ".NET 10"
}));

app.Run();


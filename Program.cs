using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnSiteApi.Data;
using OnSiteApi.Endpoints;
using OnSiteApi.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// 1. DATABASE
// =====================================================

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


// =====================================================
// 2. SUPABASE AUTH SERVICE
// =====================================================

builder.Services.AddSingleton<SupabaseAuthService>();


// =====================================================
// 3. SUPABASE JWT AUTHENTICATION
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
// 4. AUTHORIZATION
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


// =====================================================
// 5. BUILD
// =====================================================

var app = builder.Build();


// =====================================================
// 6. MIDDLEWARE
// =====================================================

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();


// =====================================================
// 7. API ENDPOINTS
// =====================================================

app.MapProfilesEndpoints();
app.MapSitesEndpoints();
app.MapAssignmentsEndpoints();
app.MapSiteUpdatesEndpoints();
app.MapTruckLogsEndpoints();


// =====================================================
// 8. ROOT / STATUS
// =====================================================

app.MapGet("/", () => Results.Ok(new
{
    status = "On Site API is running.",
    framework = ".NET 10"
}));


// =====================================================
// 9. RUN
// =====================================================

app.Run();
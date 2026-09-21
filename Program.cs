using System.Text.Json;
using System.Text.Json.Serialization;
using FirebaseAdmin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OnSiteApi.Data;
using OnSiteApi.Endpoints;
using OnSiteApi.Services;

var builder =
    WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection");

if (
    string.IsNullOrWhiteSpace(
        connectionString))
{
    throw new InvalidOperationException(
        "DefaultConnection was not found.");
}

builder.Services.AddDbContext<OnSiteDbContext>(
    options =>
        options.UseNpgsql(
            connectionString));

builder.Services.AddSingleton<SupabaseAuthService>();

builder.Services.AddSingleton<
    FirebaseNotificationService>();

// =====================================================
// JSON SERIALIZATION
// =====================================================

builder.Services.ConfigureHttpJsonOptions(
    options =>
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
    new JsonWebKeySet(
        jwksJson);

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
            IssuerSigningKeys =
                jwks.Keys
        };
});

// =====================================================
// AUTHORIZATION
// =====================================================

builder.Services.AddAuthorization(
    options =>
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

var app =
    builder.Build();

// =====================================================
// HTTP PIPELINE
// =====================================================

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    await next();

    if (
        context.Response.StatusCode ==
        StatusCodes.Status401Unauthorized)
    {
        Console.WriteLine(
            $"AUTH 401: {context.Request.Method} {context.Request.Path}");
    }
});

app.UseAuthentication();

app.UseAuthorization();

// =====================================================
// API ENDPOINTS
// =====================================================

app.MapProfilesEndpoints();

app.MapGoogleAuthEndpoints();

app.MapSitesEndpoints();

app.MapAssignmentsEndpoints();

app.MapSiteUpdatesEndpoints();

app.MapNotificationsEndpoints();

// =====================================================
// API STATUS
// =====================================================

app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                status =
                    "On Site API is running.",

                framework =
                    ".NET 10"
            }));

app.Run();
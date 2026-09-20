using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnSiteApi.Data;
using OnSiteApi.Models;

namespace OnSiteApi.Endpoints;

public static class GoogleAuthEndpoints
{
    public static void MapGoogleAuthEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/v1/auth")
                .RequireAuthorization();

        group.MapPost(
            "/google-register",
            async (
                GoogleRegistrationInput input,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var sub =
                    userClaims.FindFirst(
                        ClaimTypes.NameIdentifier)
                        ?.Value
                    ??
                    userClaims.FindFirst("sub")
                        ?.Value;

                if (!Guid.TryParse(sub, out var userId))
                {
                    return Results.Unauthorized();
                }

                var email =
                    userClaims.FindFirst(
                        ClaimTypes.Email)
                        ?.Value;

                if (string.IsNullOrWhiteSpace(email))
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "Google account email could not be determined."
                        });
                }

                email =
                    email.Trim()
                        .ToLowerInvariant();

                var existingProfile =
                    await db.Profiles
                        .FirstOrDefaultAsync(
                            p => p.Id == userId);

                if (existingProfile != null)
                {
                    if (!existingProfile.IsActive)
                    {
                        return Results.Json(
                            new
                            {
                                message =
                                    "Your OnSite account is inactive."
                            },
                            statusCode:
                                StatusCodes.Status403Forbidden);
                    }

                    return Results.Ok(
                        MapProfile(existingProfile));
                }

                var emailProfile =
                    await db.Profiles
                        .FirstOrDefaultAsync(
                            p => p.Email == email);

                if (emailProfile != null)
                {
                    return Results.Conflict(
                        new
                        {
                            message =
                                "An OnSite profile already exists for this email. Ask an administrator to link your Google account."
                        });
                }

                if (string.IsNullOrWhiteSpace(input.FullName))
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "Full name is required."
                        });
                }

                var profile =
                    new Profile
                    {
                        Id =
                            userId,

                        FullName =
                            input.FullName.Trim(),

                        Role =
                            UserRole.Foreman,

                        Email =
                            email,

                        Phone =
                            null,

                        IsActive =
                            true,

                        Password =
                            null
                    };

                db.Profiles.Add(profile);

                await db.SaveChangesAsync();

                return Results.Created(
                    $"/api/v1/profiles/{profile.Id}",
                    MapProfile(profile));
            });
    }

    private static object MapProfile(
        Profile profile)
    {
        return new
        {
            id =
                profile.Id,

            fullName =
                profile.FullName,

            role =
                profile.Role,

            phone =
                profile.Phone,

            email =
                profile.Email,

            isActive =
                profile.IsActive,

            createdAt =
                profile.CreatedAt
        };
    }
}

public record GoogleRegistrationInput(
    string FullName);
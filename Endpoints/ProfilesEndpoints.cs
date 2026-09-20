```csharp
using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnSiteApi.Data;
using OnSiteApi.Models;
using OnSiteApi.Services;

namespace OnSiteApi.Endpoints;

public static class ProfilesEndpoints
{
    public static void MapProfilesEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/v1/profiles")
                .RequireAuthorization();

        // =====================================================
        // POST /api/v1/profiles
        // =====================================================

        group.MapPost(
            "/",
            async (
                ProfileInput input,
                OnSiteDbContext db,
                SupabaseAuthService authService,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (
                    currentUser == null ||
                    currentUser.Role != UserRole.Admin)
                {
                    return Results.Json(
                        new
                        {
                            message =
                                "Forbidden: Admin access required."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                if (
                    string.IsNullOrWhiteSpace(
                        input.FullName))
                {
                    return Results.BadRequest(
                        "Full name is required.");
                }

                if (
                    string.IsNullOrWhiteSpace(
                        input.Email))
                {
                    return Results.BadRequest(
                        "Email is required.");
                }

                if (
                    !Enum.TryParse<UserRole>(
                        input.Role,
                        true,
                        out var mappedRole))
                {
                    return Results.BadRequest(
                        "Invalid role. Role must be admin, foreman, or truck_driver.");
                }

                var email =
                    input.Email
                        .Trim()
                        .ToLowerInvariant();

                var fullName =
                    input.FullName.Trim();

                var existingProfile =
                    await db.Profiles
                        .AnyAsync(
                            p =>
                                p.Email == email);

                if (existingProfile)
                {
                    return Results.Conflict(
                        "A profile with this email already exists.");
                }

                var temporaryPassword =
                    string.IsNullOrWhiteSpace(
                        input.Password)
                        ? GenerateTemporaryPassword()
                        : input.Password;

                if (
                    temporaryPassword.Length < 6)
                {
                    return Results.BadRequest(
                        "Password must be at least 6 characters long.");
                }

                Guid authUserId;

                // =================================================
                // CREATE SUPABASE AUTH USER
                // =================================================

                try
                {
                    authUserId =
                        await authService.CreateUserAsync(
                            email,
                            temporaryPassword,
                            fullName);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                ex.Message
                        });
                }

                // =================================================
                // CREATE PROFILE
                // =================================================

                try
                {
                    var newProfile =
                        new Profile
                        {
                            Id =
                                authUserId,

                            FullName =
                                fullName,

                            Role =
                                mappedRole,

                            Phone =
                                input.Phone,

                            Email =
                                email,

                            IsActive =
                                input.IsActive,

                            Password =
                                null
                        };

                    db.Profiles.Add(
                        newProfile);

                    await db.SaveChangesAsync();

                    return Results.Created(
                        $"/api/v1/profiles/{newProfile.Id}",
                        new
                        {
                            profile =
                                MapProfileResponse(
                                    newProfile),

                            temporaryPassword
                        });
                }
                catch (Exception ex)
                {
                    await authService.DeleteUserAsync(
                        authUserId);

                    return Results.Problem(
                        detail:
                            $"The Auth account was created, but the profile could not be created. The Auth account was rolled back. Error: {ex.Message}",
                        statusCode:
                            StatusCodes.Status500InternalServerError);
                }
            });

        // =====================================================
        // GET /api/v1/profiles
        // =====================================================

        group.MapGet(
            "/",
            async (
                string? role,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (
                    currentUser == null ||
                    currentUser.Role != UserRole.Admin)
                {
                    return Results.Json(
                        new
                        {
                            message =
                                "Forbidden: Admin access required."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                var query =
                    db.Profiles.AsQueryable();

                if (!string.IsNullOrEmpty(role))
                {
                    if (
                        Enum.TryParse<UserRole>(
                            role,
                            true,
                            out var filterRole))
                    {
                        query =
                            query.Where(
                                p =>
                                    p.Role ==
                                    filterRole);
                    }
                    else
                    {
                        return Results.BadRequest(
                            "Invalid role filter. Use admin, foreman, or truck_driver.");
                    }
                }

                var profiles =
                    await query.ToListAsync();

                var response =
                    profiles
                        .Select(
                            MapProfileResponse)
                        .ToList();

                return Results.Ok(
                    response);
            });

        // =====================================================
        // PATCH /api/v1/profiles/{id}
        // =====================================================

        group.MapPatch(
            "/{id:guid}",
            async (
                Guid id,
                ProfileUpdateInput input,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (
                    currentUser == null ||
                    currentUser.Role != UserRole.Admin)
                {
                    return Results.Json(
                        new
                        {
                            message =
                                "Forbidden: Admin access required."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                var profile =
                    await db.Profiles
                        .FirstOrDefaultAsync(
                            p =>
                                p.Id == id);

                if (profile == null)
                {
                    return Results.NotFound(
                        $"Profile with ID {id} not found.");
                }

                if (input.FullName != null)
                {
                    profile.FullName =
                        input.FullName.Trim();
                }

                if (input.Phone != null)
                {
                    profile.Phone =
                        input.Phone;
                }

                if (input.Email != null)
                {
                    profile.Email =
                        input.Email
                            .Trim()
                            .ToLowerInvariant();
                }

                if (input.IsActive.HasValue)
                {
                    profile.IsActive =
                        input.IsActive.Value;
                }

                if (input.Role != null)
                {
                    if (
                        Enum.TryParse<UserRole>(
                            input.Role,
                            true,
                            out var mappedRole))
                    {
                        profile.Role =
                            mappedRole;
                    }
                    else
                    {
                        return Results.BadRequest(
                            "Invalid role. Role must be admin, foreman, or truck_driver.");
                    }
                }

                await db.SaveChangesAsync();

                return Results.Ok(
                    MapProfileResponse(
                        profile));
            });

        // =====================================================
        // DELETE /api/v1/profiles/{id}
        // =====================================================

        group.MapDelete(
            "/{id:guid}",
            async (
                Guid id,
                OnSiteDbContext db,
                SupabaseAuthService authService,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (
                    currentUser == null ||
                    currentUser.Role != UserRole.Admin)
                {
                    return Results.Json(
                        new
                        {
                            message =
                                "Forbidden: Admin access required."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                var profile =
                    await db.Profiles
                        .FirstOrDefaultAsync(
                            p =>
                                p.Id == id);

                if (profile == null)
                {
                    return Results.NotFound(
                        $"Profile with ID {id} not found.");
                }

                db.Profiles.Remove(
                    profile);

                await db.SaveChangesAsync();

                await authService.DeleteUserAsync(
                    id);

                return Results.Ok(
                    new
                    {
                        message =
                            $"Profile {id} and its Supabase Auth account were deleted successfully."
                    });
            });
    }

    // =====================================================
    // API-safe profile response.
    // Navigation properties are deliberately excluded.
    // =====================================================

    private static object MapProfileResponse(
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

    private static async Task<Profile?> GetCurrentUserAsync(
        OnSiteDbContext db,
        ClaimsPrincipal user)
    {
        var sub =
            user.FindFirst(
                ClaimTypes.NameIdentifier)
                ?.Value
            ??
            user.FindFirst("sub")
                ?.Value;

        if (
            !Guid.TryParse(
                sub,
                out var id))
        {
            return null;
        }

        return await db.Profiles
            .FirstOrDefaultAsync(
                p =>
                    p.Id == id &&
                    p.IsActive);
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper =
            "ABCDEFGHJKLMNPQRSTUVWXYZ";

        const string lower =
            "abcdefghijkmnopqrstuvwxyz";

        const string numbers =
            "23456789";

        const string special =
            "!@#$%";

        var passwordChars =
            new char[12];

        passwordChars[0] =
            GetRandomCharacter(upper);

        passwordChars[1] =
            GetRandomCharacter(lower);

        passwordChars[2] =
            GetRandomCharacter(numbers);

        passwordChars[3] =
            GetRandomCharacter(special);

        const string all =
            upper +
            lower +
            numbers +
            special;

        for (
            var i = 4;
            i < passwordChars.Length;
            i++)
        {
            passwordChars[i] =
                GetRandomCharacter(all);
        }

        Shuffle(
            passwordChars);

        return new string(
            passwordChars);
    }

    private static char GetRandomCharacter(
        string characters)
    {
        var index =
            RandomNumberGenerator.GetInt32(
                characters.Length);

        return characters[index];
    }

    private static void Shuffle(
        char[] characters)
    {
        for (
            var i = characters.Length - 1;
            i > 0;
            i--)
        {
            var j =
                RandomNumberGenerator.GetInt32(
                    i + 1);

            (
                characters[i],
                characters[j]
            ) =
            (
                characters[j],
                characters[i]
            );
        }
    }
}

public record ProfileInput(
    string FullName,
    string Role,
    string? Phone,
    string Email,
    string? Password = null,
    bool IsActive = true);

public record ProfileUpdateInput(
    string? FullName,
    string? Role,
    string? Phone,
    string? Email,
    string? Password,
    bool? IsActive);
```

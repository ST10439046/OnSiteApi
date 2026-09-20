using System;
using System.Linq;
using System.Security.Claims;
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
        var group = app.MapGroup("/api/v1/profiles")
            .RequireAuthorization();

        // =====================================================
        // POST /api/v1/profiles
        // Creates a Supabase Auth user AND matching profile
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
                    await GetCurrentUserAsync(db, userClaims);

                if (currentUser == null ||
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

                if (string.IsNullOrWhiteSpace(input.FullName))
                {
                    return Results.BadRequest(
                        "Full name is required.");
                }

                if (string.IsNullOrWhiteSpace(input.Email))
                {
                    return Results.BadRequest(
                        "Email is required.");
                }

                if (string.IsNullOrWhiteSpace(input.Password))
                {
                    return Results.BadRequest(
                        "Password is required.");
                }

                if (input.Password.Length < 6)
                {
                    return Results.BadRequest(
                        "Password must be at least 6 characters long.");
                }

                if (!Enum.TryParse<UserRole>(
                        input.Role,
                        true,
                        out var mappedRole))
                {
                    return Results.BadRequest(
                        "Invalid role. Role must be admin, foreman, or truck_driver.");
                }

                var email =
                    input.Email.Trim().ToLowerInvariant();

                var existingProfile =
                    await db.Profiles
                        .AnyAsync(p => p.Email == email);

                if (existingProfile)
                {
                    return Results.Conflict(
                        "A profile with this email already exists.");
                }

                Guid authUserId;

                // =================================================
                // 1. CREATE SUPABASE AUTH USER
                // =================================================

                try
                {
                    authUserId =
                        await authService.CreateUserAsync(
                            email,
                            input.Password,
                            input.FullName);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(
                        new
                        {
                            message = ex.Message
                        });
                }

                // =================================================
                // 2. CREATE PROFILE USING AUTH UUID
                // =================================================

                try
                {
                    var newProfile = new Profile
                    {
                        Id = authUserId,
                        FullName = input.FullName.Trim(),
                        Role = mappedRole,
                        Phone = input.Phone,
                        Email = email,
                        IsActive = input.IsActive,
                        Password = null
                    };

                    db.Profiles.Add(newProfile);

                    await db.SaveChangesAsync();

                    return Results.Created(
                        $"/api/v1/profiles/{newProfile.Id}",
                        newProfile);
                }
                catch (Exception ex)
                {
                    // =============================================
                    // 3. ROLLBACK AUTH USER IF PROFILE FAILS
                    // =============================================

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

                if (currentUser == null ||
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
                    if (Enum.TryParse<UserRole>(
                            role,
                            true,
                            out var filterRole))
                    {
                        query = query.Where(
                            p => p.Role == filterRole);
                    }
                    else
                    {
                        return Results.BadRequest(
                            "Invalid role filter. Use admin, foreman, or truck_driver.");
                    }
                }

                var profiles =
                    await query.ToListAsync();

                return Results.Ok(profiles);
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

                if (currentUser == null ||
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
                            p => p.Id == id);

                if (profile == null)
                {
                    return Results.NotFound(
                        $"Profile with ID {id} not found.");
                }

                if (input.FullName != null)
                    profile.FullName = input.FullName;

                if (input.Phone != null)
                    profile.Phone = input.Phone;

                if (input.Email != null)
                    profile.Email =
                        input.Email.Trim().ToLowerInvariant();

                if (input.IsActive.HasValue)
                    profile.IsActive =
                        input.IsActive.Value;

                // Password changes should be handled through
                // Supabase Auth, not stored in public.profiles.
                // Therefore this field is intentionally ignored.

                if (input.Role != null)
                {
                    if (Enum.TryParse<UserRole>(
                            input.Role,
                            true,
                            out var mappedRole))
                    {
                        profile.Role = mappedRole;
                    }
                    else
                    {
                        return Results.BadRequest(
                            "Invalid role. Role must be admin, foreman, or truck_driver.");
                    }
                }

                await db.SaveChangesAsync();

                return Results.Ok(profile);
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

                if (currentUser == null ||
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
                            p => p.Id == id);

                if (profile == null)
                {
                    return Results.NotFound(
                        $"Profile with ID {id} not found.");
                }

                db.Profiles.Remove(profile);

                await db.SaveChangesAsync();

                // Also remove the Supabase Auth account.
                await authService.DeleteUserAsync(id);

                return Results.Ok(
                    new
                    {
                        message =
                            $"Profile {id} and its Supabase Auth account were deleted successfully."
                    });
            });
    }

    private static async Task<Profile?> GetCurrentUserAsync(
        OnSiteDbContext db,
        ClaimsPrincipal user)
    {
        var sub =
            user.FindFirst(
                ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var id))
            return null;

        return await db.Profiles
            .FirstOrDefaultAsync(
                p => p.Id == id &&
                     p.IsActive);
    }
}

public record ProfileInput(
    string FullName,
    string Role,
    string? Phone,
    string Email,
    string Password,
    bool IsActive = true);

public record ProfileUpdateInput(
    string? FullName,
    string? Role,
    string? Phone,
    string? Email,
    string? Password,
    bool? IsActive);
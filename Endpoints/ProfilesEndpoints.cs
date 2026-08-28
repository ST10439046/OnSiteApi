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

namespace OnSiteApi.Endpoints;

public static class ProfilesEndpoints
{
    public static void MapProfilesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/profiles")
            .RequireAuthorization(); // Requires general authenticated user, we will check Admin role programmatically

        // POST / - Creates a new user profile
        group.MapPost("/", async (ProfileInput input, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            if (!Enum.TryParse<UserRole>(input.Role, true, out var mappedRole))
            {
                return Results.BadRequest("Invalid role. Role must be admin, foreman, or truck_driver.");
            }

            var existing = await db.Profiles.AnyAsync(p => p.Email == input.Email || p.Id == input.Id);
            if (existing)
            {
                return Results.Conflict("A profile with this ID or Email already exists.");
            }

            var newProfile = new Profile
            {
                Id = input.Id,
                FullName = input.FullName,
                Role = mappedRole,
                Phone = input.Phone,
                Email = input.Email,
                IsActive = input.IsActive,
                Password = input.Password // Password is saved here as per prompt request
            };

            db.Profiles.Add(newProfile);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/profiles/{newProfile.Id}", newProfile);
        });

        // GET / - Retrieves all user profiles
        group.MapGet("/", async (string? role, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var query = db.Profiles.AsQueryable();

            if (!string.IsNullOrEmpty(role))
            {
                if (Enum.TryParse<UserRole>(role, true, out var filterRole))
                {
                    query = query.Where(p => p.Role == filterRole);
                }
                else
                {
                    return Results.BadRequest("Invalid role filter. Use admin, foreman, or truck_driver.");
                }
            }

            var profiles = await query.ToListAsync();
            return Results.Ok(profiles);
        });

        // PATCH /{id} - Modifies a profile
        group.MapPatch("/{id:guid}", async (Guid id, ProfileUpdateInput input, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile == null)
            {
                return Results.NotFound($"Profile with ID {id} not found.");
            }

            if (input.FullName != null) profile.FullName = input.FullName;
            if (input.Phone != null) profile.Phone = input.Phone;
            if (input.Email != null) profile.Email = input.Email;
            if (input.IsActive.HasValue) profile.IsActive = input.IsActive.Value;
            if (input.Password != null) profile.Password = input.Password;

            if (input.Role != null)
            {
                if (Enum.TryParse<UserRole>(input.Role, true, out var mappedRole))
                {
                    profile.Role = mappedRole;
                }
                else
                {
                    return Results.BadRequest("Invalid role. Role must be admin, foreman, or truck_driver.");
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(profile);
        });

        // DELETE /{id} - Removes a user profile
        group.MapDelete("/{id:guid}", async (Guid id, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile == null)
            {
                return Results.NotFound($"Profile with ID {id} not found.");
            }

            db.Profiles.Remove(profile);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = $"Profile {id} deleted successfully." });
        });
    }

    private static async Task<Profile?> GetCurrentUserAsync(OnSiteDbContext db, ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var id)) return null;
        return await db.Profiles.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
    }
}

public record ProfileInput(Guid Id, string FullName, string Role, string? Phone, string Email, string Password, bool IsActive = true);
public record ProfileUpdateInput(string? FullName, string? Role, string? Phone, string? Email, string? Password, bool? IsActive);

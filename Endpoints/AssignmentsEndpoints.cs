using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnSiteApi.Data;
using OnSiteApi.Models;
using OnSiteApi.Services;

namespace OnSiteApi.Endpoints;

public static class AssignmentsEndpoints
{
    public static void MapAssignmentsEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/v1/assignments")
                .RequireAuthorization();

        // =====================================================
        // POST /
        // Maps a foreman to a site.
        // Admin only.
        // =====================================================

        group.MapPost(
            "/",
            async (
                AssignmentInput input,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims,
                FirebaseNotificationService firebase) =>
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

                // =====================================================
                // Verify site exists
                // =====================================================

                var site =
                    await db.Sites
                        .FirstOrDefaultAsync(
                            s =>
                                s.Id == input.SiteId);

                if (site == null)
                {
                    return Results.BadRequest(
                        "Site not found.");
                }

                // =====================================================
                // Verify foreman exists
                // =====================================================

                var foreman =
                    await db.Profiles
                        .FirstOrDefaultAsync(
                            p =>
                                p.Id == input.ForemanId);

                if (foreman == null)
                {
                    return Results.BadRequest(
                        "Foreman not found.");
                }

                if (
                    foreman.Role !=
                    UserRole.Foreman)
                {
                    return Results.BadRequest(
                        "Target user is not a foreman.");
                }

                if (!foreman.IsActive)
                {
                    return Results.BadRequest(
                        "Foreman is not active.");
                }

                // =====================================================
                // Check if assignment already exists
                // =====================================================

                var existing =
                    await db.SiteForemen
                        .AnyAsync(
                            sf =>
                                sf.SiteId ==
                                    input.SiteId &&
                                sf.ForemanId ==
                                    input.ForemanId);

                if (existing)
                {
                    return Results.Conflict(
                        "Foreman is already assigned to this site.");
                }

                // =====================================================
                // Create assignment
                // =====================================================

                var assignment =
                    new SiteForeman
                    {
                        SiteId =
                            input.SiteId,

                        ForemanId =
                            input.ForemanId
                    };

                db.SiteForemen.Add(
                    assignment);

                await db.SaveChangesAsync();

                // =====================================================
                // Create notification for foreman
                // =====================================================

                var title =
                    "New Site Assignment";

                var message =
                    $"You have been assigned to {site.Name}.";

                var data =
                    new Dictionary<string, string>
                    {
                        ["type"] =
                            "site_assigned",

                        ["site_id"] =
                            site.Id.ToString(),

                        ["foreman_id"] =
                            foreman.Id.ToString()
                    };

                await NotificationsEndpoints
                    .CreateNotificationAsync(
                        db,
                        foreman.Id,
                        "site_assigned",
                        title,
                        message,
                        data);

                // =====================================================
                // Send push notification.
                // This respects the foreman's push preference.
                // =====================================================

                await NotificationsEndpoints
                    .SendNotificationToUserAsync(
                        db,
                        firebase,
                        foreman.Id,
                        title,
                        message,
                        "site_assigned",
                        data);

                return Results.Created(
                    $"/api/v1/assignments?site_id={assignment.SiteId}&foreman_id={assignment.ForemanId}",
                    new
                    {
                        assignment.SiteId,
                        assignment.ForemanId
                    });
            });

        // =====================================================
        // GET /
        // Retrieves assignment lists.
        // Admin only.
        // =====================================================

        group.MapGet(
            "/",
            async (
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

                var assignments =
                    await db.SiteForemen
                        .Select(
                            sf =>
                                new
                                {
                                    sf.SiteId,

                                    SiteName =
                                        sf.Site != null
                                            ? sf.Site.Name
                                            : string.Empty,

                                    sf.ForemanId,

                                    ForemanName =
                                        sf.Foreman != null
                                            ? sf.Foreman.FullName
                                            : string.Empty
                                })
                        .ToListAsync();

                return Results.Ok(
                    assignments);
            });

        // =====================================================
        // DELETE /
        // Removes an assignment.
        // Admin only.
        // =====================================================

        group.MapDelete(
            "/",
            async (
                Guid site_id,
                Guid foreman_id,
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

                var assignment =
                    await db.SiteForemen
                        .FirstOrDefaultAsync(
                            sf =>
                                sf.SiteId ==
                                    site_id &&
                                sf.ForemanId ==
                                    foreman_id);

                if (assignment == null)
                {
                    return Results.NotFound(
                        "Assignment not found.");
                }

                db.SiteForemen.Remove(
                    assignment);

                await db.SaveChangesAsync();

                return Results.Ok(
                    new
                    {
                        message =
                            "Assignment removed successfully."
                    });
            });
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
}

public record AssignmentInput(
    Guid SiteId,
    Guid ForemanId);
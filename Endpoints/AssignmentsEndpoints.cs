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

public static class AssignmentsEndpoints
{
    public static void MapAssignmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assignments")
            .RequireAuthorization();

        // POST / - Maps a foreman to a site (Admin Only)
        group.MapPost("/", async (AssignmentInput input, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            // Verify site exists
            var siteExists = await db.Sites.AnyAsync(s => s.Id == input.SiteId);
            if (!siteExists)
            {
                return Results.BadRequest("Site not found.");
            }

            // Verify foreman exists and is actually a foreman
            var foreman = await db.Profiles.FirstOrDefaultAsync(p => p.Id == input.ForemanId);
            if (foreman == null)
            {
                return Results.BadRequest("Foreman not found.");
            }
            if (foreman.Role != UserRole.Foreman)
            {
                return Results.BadRequest("Target user is not a foreman.");
            }

            // Check if assignment already exists
            var existing = await db.SiteForemen.AnyAsync(sf => sf.SiteId == input.SiteId && sf.ForemanId == input.ForemanId);
            if (existing)
            {
                return Results.Conflict("Foreman is already assigned to this site.");
            }

            var assignment = new SiteForeman
            {
                SiteId = input.SiteId,
                ForemanId = input.ForemanId
            };

            db.SiteForemen.Add(assignment);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/assignments?site_id={assignment.SiteId}&foreman_id={assignment.ForemanId}", new
            {
                assignment.SiteId,
                assignment.ForemanId
            });
        });

        // GET / - Retrieves assignment lists (Admin Only)
        group.MapGet("/", async (OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var assignments = await db.SiteForemen
                .Select(sf => new
                {
                    sf.SiteId,
                    SiteName = sf.Site != null ? sf.Site.Name : string.Empty,
                    sf.ForemanId,
                    ForemanName = sf.Foreman != null ? sf.Foreman.FullName : string.Empty
                })
                .ToListAsync();

            return Results.Ok(assignments);
        });

        // DELETE / - Removes an assignment (Admin Only)
        group.MapDelete("/", async (Guid site_id, Guid foreman_id, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var assignment = await db.SiteForemen
                .FirstOrDefaultAsync(sf => sf.SiteId == site_id && sf.ForemanId == foreman_id);

            if (assignment == null)
            {
                return Results.NotFound("Assignment not found.");
            }

            db.SiteForemen.Remove(assignment);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Assignment removed successfully." });
        });
    }

    private static async Task<Profile?> GetCurrentUserAsync(OnSiteDbContext db, ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var id)) return null;
        return await db.Profiles.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
    }
}

public record AssignmentInput(Guid SiteId, Guid ForemanId);

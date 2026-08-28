using System;
using System.Collections.Generic;
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

public static class SitesEndpoints
{
    public static void MapSitesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sites")
            .RequireAuthorization();

        // POST / - Registers a new construction site (Admin Only)
        group.MapPost("/", async (SiteInput input, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var newSite = new Site
            {
                Name = input.Name,
                Address = input.Address,
                IsActive = input.IsActive
            };

            db.Sites.Add(newSite);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/sites/{newSite.Id}", newSite);
        });

        // GET / - Retrieves sites with daily submission status
        group.MapGet("/", async (OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Foreman))
            {
                return Results.Json(new { message = "Forbidden: Admin or Foreman access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            List<Site> sites;
            if (currentUser.Role == UserRole.Admin)
            {
                sites = await db.Sites.ToListAsync();
            }
            else
            {
                // Foreman restricted to assigned sites
                sites = await db.Sites
                    .Where(s => db.SiteForemen.Any(sf => sf.SiteId == s.Id && sf.ForemanId == currentUser.Id))
                    .ToListAsync();
            }

            // Get today's updates
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var todayUpdateSiteIds = await db.SiteUpdates
                .Where(su => su.UpdateDate == today)
                .Select(su => su.SiteId)
                .ToListAsync();

            var responseList = sites.Select(s => new
            {
                s.Id,
                s.Name,
                s.Address,
                s.IsActive,
                s.CreatedAt,
                Status = todayUpdateSiteIds.Contains(s.Id) ? "Submitted ✓" : "Needs update"
            });

            return Results.Ok(responseList);
        });

        // PATCH /{id} - Modifies site status or details (Admin Only)
        group.MapPatch("/{id:guid}", async (Guid id, SiteUpdateInput input, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == id);
            if (site == null)
            {
                return Results.NotFound($"Site with ID {id} not found.");
            }

            if (input.Name != null) site.Name = input.Name;
            if (input.Address != null) site.Address = input.Address;
            if (input.IsActive.HasValue) site.IsActive = input.IsActive.Value;

            await db.SaveChangesAsync();
            return Results.Ok(site);
        });

        // DELETE /{id} - Deletes site (Admin Only)
        group.MapDelete("/{id:guid}", async (Guid id, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Admin)
            {
                return Results.Json(new { message = "Forbidden: Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == id);
            if (site == null)
            {
                return Results.NotFound($"Site with ID {id} not found.");
            }

            db.Sites.Remove(site);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = $"Site {id} deleted successfully." });
        });
    }

    private static async Task<Profile?> GetCurrentUserAsync(OnSiteDbContext db, ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var id)) return null;
        return await db.Profiles.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
    }
}

public record SiteInput(string Name, string Address, bool IsActive = true);
public record SiteUpdateInput(string? Name, string? Address, bool? IsActive);

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

public static class SiteUpdatesEndpoints
{
    public static void MapSiteUpdatesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/site-updates")
            .RequireAuthorization();

        // POST / - Submits a daily site update (Foreman Only)
        group.MapPost("/", async (SiteUpdateInputModel input, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.Foreman)
            {
                return Results.Json(new { message = "Forbidden: Foreman access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            // Verify foreman is assigned to this site
            var isAssigned = await db.SiteForemen.AnyAsync(sf => sf.SiteId == input.SiteId && sf.ForemanId == currentUser.Id);
            if (!isAssigned)
            {
                return Results.Json(new { message = "Forbidden: Foreman is not assigned to this site." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Calculations
            int actualLabor = input.Bricklayers + input.Plasterers + input.Pavers;
            int variance = actualLabor - input.ForecastedLabor;

            // Check if update already exists for today on this site (Same-day Override)
            var existingUpdate = await db.SiteUpdates
                .Include(su => su.UpdatePhotos)
                .FirstOrDefaultAsync(su => su.SiteId == input.SiteId && su.UpdateDate == today);

            if (existingUpdate != null)
            {
                // Update existing record
                existingUpdate.ForemanId = currentUser.Id; // override with current foreman performing the action
                existingUpdate.ForecastedLabor = input.ForecastedLabor;
                existingUpdate.Bricklayers = input.Bricklayers;
                existingUpdate.Plasterers = input.Plasterers;
                existingUpdate.Pavers = input.Pavers;
                existingUpdate.ActualLabor = actualLabor;
                existingUpdate.StaffNames = input.StaffNames;
                existingUpdate.PowerTools = input.PowerTools;
                existingUpdate.PlantMachines = input.PlantMachines;
                existingUpdate.Notes = input.Notes;

                // Handle photos replacement if provided
                if (input.Photos != null)
                {
                    // Clean old photos
                    db.UpdatePhotos.RemoveRange(existingUpdate.UpdatePhotos);

                    // Add new ones
                    foreach (var photoInput in input.Photos)
                    {
                        existingUpdate.UpdatePhotos.Add(new UpdatePhoto
                        {
                            StoragePath = photoInput.StoragePath,
                            Caption = photoInput.Caption
                        });
                    }
                }

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    message = "Daily update overridden successfully.",
                    update = existingUpdate,
                    variance
                });
            }
            else
            {
                // Create new record
                var newUpdate = new SiteUpdate
                {
                    SiteId = input.SiteId,
                    ForemanId = currentUser.Id,
                    UpdateDate = today,
                    ForecastedLabor = input.ForecastedLabor,
                    Bricklayers = input.Bricklayers,
                    Plasterers = input.Plasterers,
                    Pavers = input.Pavers,
                    ActualLabor = actualLabor,
                    StaffNames = input.StaffNames,
                    PowerTools = input.PowerTools,
                    PlantMachines = input.PlantMachines,
                    Notes = input.Notes
                };

                if (input.Photos != null)
                {
                    foreach (var photoInput in input.Photos)
                    {
                        newUpdate.UpdatePhotos.Add(new UpdatePhoto
                        {
                            StoragePath = photoInput.StoragePath,
                            Caption = photoInput.Caption
                        });
                    }
                }

                db.SiteUpdates.Add(newUpdate);
                await db.SaveChangesAsync();

                return Results.Created($"/api/v1/site-updates/{newUpdate.Id}", new
                {
                    message = "Daily update submitted successfully.",
                    update = newUpdate,
                    variance
                });
            }
        });

        // GET / - Retrieves updates (Admin and Foreman access)
        group.MapGet("/", async (DateOnly? start_date, DateOnly? end_date, Guid? site_id, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Foreman))
            {
                return Results.Json(new { message = "Forbidden: Admin or Foreman access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var query = db.SiteUpdates
                .Include(su => su.UpdatePhotos)
                .AsQueryable();

            if (currentUser.Role == UserRole.Foreman)
            {
                // Foremen only see updates for their assigned sites
                var assignedSiteIds = await db.SiteForemen
                    .Where(sf => sf.ForemanId == currentUser.Id)
                    .Select(sf => sf.SiteId)
                    .ToListAsync();

                query = query.Where(su => assignedSiteIds.Contains(su.SiteId));
            }
            else
            {
                // Admin filters
                if (site_id.HasValue)
                {
                    query = query.Where(su => su.SiteId == site_id.Value);
                }
            }

            if (start_date.HasValue)
            {
                query = query.Where(su => su.UpdateDate >= start_date.Value);
            }

            if (end_date.HasValue)
            {
                query = query.Where(su => su.UpdateDate <= end_date.Value);
            }

            var updates = await query.ToListAsync();

            // Calculate and return with variance dynamically
            var response = updates.Select(u => new
            {
                u.Id,
                u.SiteId,
                u.ForemanId,
                u.UpdateDate,
                u.ForecastedLabor,
                u.Bricklayers,
                u.Plasterers,
                u.Pavers,
                u.ActualLabor,
                u.StaffNames,
                u.PowerTools,
                u.PlantMachines,
                u.Notes,
                u.CreatedAt,
                u.UpdatePhotos,
                Variance = u.ActualLabor - u.ForecastedLabor
            });

            return Results.Ok(response);
        });
    }

    private static async Task<Profile?> GetCurrentUserAsync(OnSiteDbContext db, ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var id)) return null;
        return await db.Profiles.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
    }
}

public record PhotoInputModel(string StoragePath, string? Caption);

public record SiteUpdateInputModel(
    Guid SiteId,
    int ForecastedLabor,
    int Bricklayers,
    int Plasterers,
    int Pavers,
    string? StaffNames,
    string? PowerTools,
    string? PlantMachines,
    string? Notes,
    List<PhotoInputModel>? Photos
);

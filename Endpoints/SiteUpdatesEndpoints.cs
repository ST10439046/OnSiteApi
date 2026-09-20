using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnSiteApi.Data;
using OnSiteApi.Models;
using OnSiteApi.Services;

namespace OnSiteApi.Endpoints;

public static class SiteUpdatesEndpoints
{
    public static void MapSiteUpdatesEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/v1/site-updates")
                .RequireAuthorization();

        // =====================================================
        // POST /api/v1/site-updates
        // Create or update today's report
        // =====================================================

        group.MapPost(
            "/",
            async (
                SiteUpdateInputModel input,
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
                    currentUser.Role != UserRole.Foreman)
                {
                    return Results.Json(
                        new
                        {
                            message =
                                "Forbidden: Foreman access required."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                if (input.SiteId == Guid.Empty)
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "A valid site is required."
                        });
                }

                var isAssigned =
                    await db.SiteForemen.AnyAsync(
                        sf =>
                            sf.SiteId == input.SiteId &&
                            sf.ForemanId == currentUser.Id);

                if (!isAssigned)
                {
                    return Results.Json(
                        new
                        {
                            message =
                                "Forbidden: Foreman is not assigned to this site."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                var site =
                    await db.Sites
                        .FirstOrDefaultAsync(
                            s =>
                                s.Id == input.SiteId);

                if (site == null)
                {
                    return Results.NotFound(
                        new
                        {
                            message =
                                "Site not found."
                        });
                }

                var staff =
                    ParseStaff(input.StaffNames);

                var bricklayers =
                    staff.Count(
                        x =>
                            x.Job.Equals(
                                "bricklayers",
                                StringComparison.OrdinalIgnoreCase));

                var plasterers =
                    staff.Count(
                        x =>
                            x.Job.Equals(
                                "plasterers",
                                StringComparison.OrdinalIgnoreCase));

                var pavers =
                    staff.Count(
                        x =>
                            x.Job.Equals(
                                "pavers",
                                StringComparison.OrdinalIgnoreCase));

                var actualLabor =
                    staff.Count;

                var forecastedLabor =
                    actualLabor;

                var today =
                    DateOnly.FromDateTime(
                        DateTime.UtcNow);

                var existingUpdate =
                    await db.SiteUpdates
                        .Include(
                            su =>
                                su.UpdatePhotos)
                        .FirstOrDefaultAsync(
                            su =>
                                su.SiteId == input.SiteId &&
                                su.UpdateDate == today);

                // =====================================================
                // UPDATE EXISTING REPORT
                // =====================================================

                if (existingUpdate != null)
                {
                    existingUpdate.ForemanId =
                        currentUser.Id;

                    existingUpdate.ForecastedLabor =
                        forecastedLabor;

                    existingUpdate.Bricklayers =
                        bricklayers;

                    existingUpdate.Plasterers =
                        plasterers;

                    existingUpdate.Pavers =
                        pavers;

                    existingUpdate.ActualLabor =
                        actualLabor;

                    existingUpdate.StaffNames =
                        input.StaffNames;

                    existingUpdate.PowerTools =
                        input.PowerTools;

                    existingUpdate.PlantMachines =
                        input.PlantMachines;

                    existingUpdate.Notes =
                        input.Notes;

                    if (input.Photos != null)
                    {
                        db.UpdatePhotos.RemoveRange(
                            existingUpdate.UpdatePhotos);

                        existingUpdate.UpdatePhotos.Clear();

                        foreach (
                            var photo
                            in input.Photos)
                        {
                            existingUpdate.UpdatePhotos.Add(
                                new UpdatePhoto
                                {
                                    PhotoData =
                                        photo.PhotoData,

                                    Caption =
                                        photo.Caption
                                });
                        }
                    }

                    await db.SaveChangesAsync();

                    // =====================================================
                    // NOTIFY ADMINS
                    // =====================================================

                    await NotifyAdminsAboutReportAsync(
                        db,
                        firebase,
                        currentUser,
                        site,
                        existingUpdate,
                        true);

                    return Results.Ok(
                        MapUpdateResponse(
                            existingUpdate));
                }

                // =====================================================
                // CREATE NEW REPORT
                // =====================================================

                var newUpdate =
                    new SiteUpdate
                    {
                        SiteId =
                            input.SiteId,

                        ForemanId =
                            currentUser.Id,

                        UpdateDate =
                            today,

                        ForecastedLabor =
                            forecastedLabor,

                        Bricklayers =
                            bricklayers,

                        Plasterers =
                            plasterers,

                        Pavers =
                            pavers,

                        ActualLabor =
                            actualLabor,

                        StaffNames =
                            input.StaffNames,

                        PowerTools =
                            input.PowerTools,

                        PlantMachines =
                            input.PlantMachines,

                        Notes =
                            input.Notes
                    };

                if (input.Photos != null)
                {
                    foreach (
                        var photo
                        in input.Photos)
                    {
                        newUpdate.UpdatePhotos.Add(
                            new UpdatePhoto
                            {
                                PhotoData =
                                    photo.PhotoData,

                                Caption =
                                    photo.Caption
                            });
                    }
                }

                db.SiteUpdates.Add(
                    newUpdate);

                await db.SaveChangesAsync();

                // =====================================================
                // NOTIFY ADMINS
                // =====================================================

                await NotifyAdminsAboutReportAsync(
                    db,
                    firebase,
                    currentUser,
                    site,
                    newUpdate,
                    false);

                return Results.Created(
                    $"/api/v1/site-updates/{newUpdate.Id}",
                    MapUpdateResponse(newUpdate));
            });

        // =====================================================
        // GET /api/v1/site-updates
        // =====================================================

        group.MapGet(
            "/",
            async (
                DateOnly? start_date,
                DateOnly? end_date,
                Guid? site_id,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (
                    currentUser == null ||
                    (
                        currentUser.Role != UserRole.Admin &&
                        currentUser.Role != UserRole.Foreman
                    ))
                {
                    return Results.Json(
                        new
                        {
                            message =
                                "Forbidden: Admin or Foreman access required."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                var query =
                    db.SiteUpdates
                        .Include(
                            su =>
                                su.UpdatePhotos)
                        .AsQueryable();

                if (
                    currentUser.Role ==
                    UserRole.Foreman)
                {
                    var assignedSiteIds =
                        await db.SiteForemen
                            .Where(
                                sf =>
                                    sf.ForemanId ==
                                    currentUser.Id)
                            .Select(
                                sf =>
                                    sf.SiteId)
                            .ToListAsync();

                    query =
                        query.Where(
                            su =>
                                assignedSiteIds.Contains(
                                    su.SiteId));
                }
                else if (site_id.HasValue)
                {
                    query =
                        query.Where(
                            su =>
                                su.SiteId ==
                                site_id.Value);
                }

                if (start_date.HasValue)
                {
                    query =
                        query.Where(
                            su =>
                                su.UpdateDate >=
                                start_date.Value);
                }

                if (end_date.HasValue)
                {
                    query =
                        query.Where(
                            su =>
                                su.UpdateDate <=
                                end_date.Value);
                }

                var updates =
                    await query.ToListAsync();

                var response =
                    updates
                        .Select(
                            MapUpdateResponse)
                        .ToList();

                return Results.Ok(
                    response);
            });
    }

    // =====================================================
    // Notify every active admin about a submitted report.
    // A notification record is always created.
    // Push delivery respects notification preferences.
    // =====================================================

    private static async Task NotifyAdminsAboutReportAsync(
        OnSiteDbContext db,
        FirebaseNotificationService firebase,
        Profile foreman,
        Site site,
        SiteUpdate update,
        bool isUpdate)
    {
        var admins =
            await db.Profiles
                .Where(
                    p =>
                        p.Role == UserRole.Admin &&
                        p.IsActive)
                .ToListAsync();

        var title =
            isUpdate
                ? "Daily Report Updated"
                : "Daily Report Submitted";

        var message =
            isUpdate
                ? $"{foreman.FullName} updated today's report for {site.Name}."
                : $"{foreman.FullName} submitted today's report for {site.Name}.";

        var data =
            new Dictionary<string, string>
            {
                ["type"] =
                    "daily_report_submitted",

                ["site_id"] =
                    update.SiteId.ToString(),

                ["update_id"] =
                    update.Id.ToString(),

                ["foreman_id"] =
                    update.ForemanId.ToString()
            };

        foreach (var admin in admins)
        {
            await NotificationsEndpoints.CreateNotificationAsync(
                db,
                admin.Id,
                "daily_report_submitted",
                title,
                message,
                data);

            await NotificationsEndpoints.SendNotificationToUserAsync(
                db,
                firebase,
                admin.Id,
                title,
                message,
                "daily_report_submitted",
                data);
        }
    }

    // =====================================================
    // Convert EF entity to API-safe response.
    // Never return SiteUpdate directly because its navigation
    // properties can create circular JSON references.
    // =====================================================

    private static object MapUpdateResponse(
        SiteUpdate update)
    {
        return new
        {
            id =
                update.Id,

            siteId =
                update.SiteId,

            foremanId =
                update.ForemanId,

            updateDate =
                update.UpdateDate,

            forecastedLabor =
                update.ForecastedLabor,

            bricklayers =
                update.Bricklayers,

            plasterers =
                update.Plasterers,

            pavers =
                update.Pavers,

            actualLabor =
                update.ActualLabor,

            staffNames =
                update.StaffNames,

            powerTools =
                update.PowerTools,

            plantMachines =
                update.PlantMachines,

            notes =
                update.Notes,

            createdAt =
                update.CreatedAt,

            updatePhotos =
                update.UpdatePhotos
                    .Select(
                        photo =>
                            new
                            {
                                id =
                                    photo.Id,

                                updateId =
                                    photo.UpdateId,

                                photoData =
                                    photo.PhotoData,

                                caption =
                                    photo.Caption,

                                createdAt =
                                    photo.CreatedAt
                            })
                    .ToList(),

            variance =
                update.ActualLabor -
                update.ForecastedLabor
        };
    }

    private static List<StaffInputModel> ParseStaff(
        string? staffNames)
    {
        if (
            string.IsNullOrWhiteSpace(
                staffNames))
        {
            return new List<StaffInputModel>();
        }

        try
        {
            var staff =
                JsonSerializer.Deserialize<
                    List<StaffInputModel>>(
                    staffNames,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    });

            return staff ??
                new List<StaffInputModel>();
        }
        catch
        {
            return new List<StaffInputModel>();
        }
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

public record StaffInputModel(
    string Name,
    string Job);

public record PhotoInputModel(
    string PhotoData,
    string? Caption);

public record SiteUpdateInputModel(
    Guid SiteId,
    string? StaffNames,
    string? PowerTools,
    string? PlantMachines,
    string? Notes,
    List<PhotoInputModel>? Photos);
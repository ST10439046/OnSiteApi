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

public static class TruckLogsEndpoints
{
    public static void MapTruckLogsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/truck-logs")
            .RequireAuthorization();

        // POST / - Submits a haulage trip (Driver Only)
        group.MapPost("/", async (TruckLogInput input, OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || currentUser.Role != UserRole.TruckDriver)
            {
                return Results.Json(new { message = "Forbidden: Driver access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            // Validation: Ending mileage cannot be less than starting mileage
            if (input.MileageAfter.HasValue && input.MileageBefore.HasValue)
            {
                if (input.MileageAfter.Value < input.MileageBefore.Value)
                {
                    return Results.BadRequest("Ending mileage cannot be less than starting mileage");
                }
            }

            // Calculations
            decimal distanceDrivenKm = 0;
            if (input.MileageAfter.HasValue && input.MileageBefore.HasValue)
            {
                distanceDrivenKm = input.MileageAfter.Value - input.MileageBefore.Value;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var log = new TruckLog
            {
                DriverId = currentUser.Id,
                DriverName = currentUser.FullName, // Driver Bind: automatically populate from profile
                Registration = input.Registration,
                TruckSize = input.TruckSize,
                LoadType = input.LoadType,
                ArrivalTime = input.ArrivalTime,
                SiteName = input.SiteName,
                LeavingTime = input.LeavingTime,
                DieselLitres = input.DieselLitres,
                DieselLocation = input.DieselLocation,
                MileageBefore = input.MileageBefore,
                MileageAfter = input.MileageAfter,
                LogDate = today
            };

            db.TruckLogs.Add(log);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/truck-logs/{log.Id}", new
            {
                message = "Truck log submitted successfully.",
                log,
                distance_driven_km = distanceDrivenKm
            });
        });

        // GET / - Retrieves truck logs (Admin and Driver access)
        group.MapGet("/", async (OnSiteDbContext db, ClaimsPrincipal userClaims) =>
        {
            var currentUser = await GetCurrentUserAsync(db, userClaims);
            if (currentUser == null || (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.TruckDriver))
            {
                return Results.Json(new { message = "Forbidden: Admin or Driver access required." }, statusCode: StatusCodes.Status403Forbidden);
            }

            var query = db.TruckLogs.AsQueryable();

            if (currentUser.Role == UserRole.TruckDriver)
            {
                // Drivers restricted to their own logs
                query = query.Where(l => l.DriverId == currentUser.Id);
            }

            var logs = await query.ToListAsync();

            // Append distance driven dynamically in the response
            var response = logs.Select(l => new
            {
                l.Id,
                l.DriverId,
                l.DriverName,
                l.Registration,
                l.TruckSize,
                l.LoadType,
                l.ArrivalTime,
                l.SiteName,
                l.LeavingTime,
                l.DieselLitres,
                l.DieselLocation,
                l.MileageBefore,
                l.MileageAfter,
                l.LogDate,
                l.CreatedAt,
                DistanceDrivenKm = (l.MileageAfter.HasValue && l.MileageBefore.HasValue) ? (l.MileageAfter.Value - l.MileageBefore.Value) : 0
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

public record TruckLogInput(
    string Registration,
    string TruckSize,
    string LoadType,
    string ArrivalTime,
    string SiteName,
    string LeavingTime,
    decimal? DieselLitres,
    string? DieselLocation,
    decimal? MileageBefore,
    decimal? MileageAfter
);

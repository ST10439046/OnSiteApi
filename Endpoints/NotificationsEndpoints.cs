using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnSiteApi.Data;
using OnSiteApi.Models;
using OnSiteApi.Services;

namespace OnSiteApi.Endpoints;

public static class NotificationsEndpoints
{
    public static void MapNotificationsEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/v1/notifications")
                .RequireAuthorization();

        // =====================================================
        // POST /api/v1/notifications/device-token
        // Register or update the current user's FCM token.
        // =====================================================

        group.MapPost(
            "/device-token",
            async (
                DeviceTokenInput input,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                if (
                    string.IsNullOrWhiteSpace(
                        input.Token))
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "FCM token is required."
                        });
                }

                var token =
                    input.Token.Trim();

                var existingToken =
                    await db.DeviceTokens
                        .FirstOrDefaultAsync(
                            x =>
                                x.Token == token);

                if (existingToken != null)
                {
                    existingToken.UserId =
                        currentUser.Id;

                    existingToken.Platform =
                        string.IsNullOrWhiteSpace(
                            input.Platform)
                            ? "android"
                            : input.Platform;

                    existingToken.IsActive =
                        true;

                    existingToken.UpdatedAt =
                        DateTime.UtcNow;
                }
                else
                {
                    db.DeviceTokens.Add(
                        new DeviceToken
                        {
                            UserId =
                                currentUser.Id,

                            Token =
                                token,

                            Platform =
                                string.IsNullOrWhiteSpace(
                                    input.Platform)
                                    ? "android"
                                    : input.Platform,

                            IsActive =
                                true,

                            CreatedAt =
                                DateTime.UtcNow,

                            UpdatedAt =
                                DateTime.UtcNow
                        });
                }

                await db.SaveChangesAsync();

                return Results.Ok(
                    new
                    {
                        message =
                            "Device token registered successfully."
                    });
            });

        // =====================================================
        // DELETE /api/v1/notifications/device-token
        // Deactivate current device token.
        // =====================================================

        group.MapDelete(
            "/device-token",
            async (
                string token,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                var deviceToken =
                    await db.DeviceTokens
                        .FirstOrDefaultAsync(
                            x =>
                                x.UserId ==
                                    currentUser.Id &&
                                x.Token ==
                                    token);

                if (deviceToken == null)
                {
                    return Results.NotFound(
                        new
                        {
                            message =
                                "Device token not found."
                        });
                }

                deviceToken.IsActive =
                    false;

                deviceToken.UpdatedAt =
                    DateTime.UtcNow;

                await db.SaveChangesAsync();

                return Results.Ok(
                    new
                    {
                        message =
                            "Device token deactivated."
                    });
            });

        // =====================================================
        // GET /api/v1/notifications
        // =====================================================

        group.MapGet(
            "/",
            async (
                int? limit,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                var take =
                    Math.Clamp(
                        limit ?? 50,
                        1,
                        100);

                var notifications =
                    await db.Notifications
                        .Where(
                            x =>
                                x.UserId ==
                                currentUser.Id)
                        .OrderByDescending(
                            x =>
                                x.CreatedAt)
                        .Take(take)
                        .Select(
                            x =>
                                new
                                {
                                    id = x.Id,
                                    type = x.Type,
                                    title = x.Title,
                                    message = x.Message,
                                    data = x.Data,
                                    isRead = x.IsRead,
                                    createdAt = x.CreatedAt
                                })
                        .ToListAsync();

                return Results.Ok(
                    notifications);
            });

        // =====================================================
        // GET /api/v1/notifications/unread-count
        // =====================================================

        group.MapGet(
            "/unread-count",
            async (
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                var count =
                    await db.Notifications
                        .CountAsync(
                            x =>
                                x.UserId ==
                                    currentUser.Id &&
                                !x.IsRead);

                return Results.Ok(
                    new
                    {
                        count
                    });
            });

        // =====================================================
        // POST /api/v1/notifications/{id}/read
        // =====================================================

        group.MapPost(
            "/{id:guid}/read",
            async (
                Guid id,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                var notification =
                    await db.Notifications
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id == id &&
                                x.UserId ==
                                    currentUser.Id);

                if (notification == null)
                {
                    return Results.NotFound(
                        new
                        {
                            message =
                                "Notification not found."
                        });
                }

                notification.IsRead =
                    true;

                await db.SaveChangesAsync();

                return Results.Ok(
                    new
                    {
                        message =
                            "Notification marked as read."
                    });
            });

        // =====================================================
        // POST /api/v1/notifications/read-all
        // =====================================================

        group.MapPost(
            "/read-all",
            async (
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                var unread =
                    await db.Notifications
                        .Where(
                            x =>
                                x.UserId ==
                                    currentUser.Id &&
                                !x.IsRead)
                        .ToListAsync();

                foreach (var notification in unread)
                {
                    notification.IsRead =
                        true;
                }

                await db.SaveChangesAsync();

                return Results.Ok(
                    new
                    {
                        message =
                            "All notifications marked as read.",
                        count =
                            unread.Count
                    });
            });

        // =====================================================
        // GET /api/v1/notifications/preferences
        // =====================================================

        group.MapGet(
            "/preferences",
            async (
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                var preference =
                    await GetOrCreatePreferenceAsync(
                        db,
                        currentUser.Id);

                return Results.Ok(
                    new
                    {
                        pushEnabled =
                            preference.PushEnabled
                    });
            });

        // =====================================================
        // PUT /api/v1/notifications/preferences
        // =====================================================

        group.MapPut(
            "/preferences",
            async (
                NotificationPreferenceInput input,
                OnSiteDbContext db,
                ClaimsPrincipal userClaims) =>
            {
                var currentUser =
                    await GetCurrentUserAsync(
                        db,
                        userClaims);

                if (currentUser == null)
                {
                    return Results.Unauthorized();
                }

                var preference =
                    await GetOrCreatePreferenceAsync(
                        db,
                        currentUser.Id);

                preference.PushEnabled =
                    input.PushEnabled;

                preference.UpdatedAt =
                    DateTime.UtcNow;

                await db.SaveChangesAsync();

                return Results.Ok(
                    new
                    {
                        pushEnabled =
                            preference.PushEnabled
                    });
            });
    }

    // =====================================================
    // Creates a notification record.
    // This will be used by report/assignment events.
    // =====================================================

    public static async Task<Notification>
        CreateNotificationAsync(
            OnSiteDbContext db,
            Guid userId,
            string type,
            string title,
            string message,
            Dictionary<string, string>?
                data = null)
    {
        var notification =
            new Notification
            {
                UserId =
                    userId,

                Type =
                    type,

                Title =
                    title,

                Message =
                    message,

                Data =
                    JsonSerializer.Serialize(
                        data ??
                        new Dictionary<string, string>()),

                IsRead =
                    false,

                CreatedAt =
                    DateTime.UtcNow
            };

        db.Notifications.Add(
            notification);

        await db.SaveChangesAsync();

        return notification;
    }

    public static async Task
        SendNotificationToUserAsync(
            OnSiteDbContext db,
            FirebaseNotificationService firebase,
            Guid userId,
            string title,
            string message,
            string type,
            Dictionary<string, string>?
                data = null)
    {
        var preference =
            await GetOrCreatePreferenceAsync(
                db,
                userId);

        if (!preference.PushEnabled)
        {
            return;
        }

        var tokens =
            await db.DeviceTokens
                .Where(
                    x =>
                        x.UserId == userId &&
                        x.IsActive)
                .Select(
                    x =>
                        x.Token)
                .ToListAsync();

        foreach (var token in tokens)
        {
            var sent =
                await firebase.SendNotificationAsync(
                    token,
                    title,
                    message,
                    data);

            if (!sent)
            {
                // Keep the token for now.
                // It can be cleaned up later when
                // Firebase reports an invalid token.
            }
        }
    }

    private static async Task<NotificationPreference>
        GetOrCreatePreferenceAsync(
            OnSiteDbContext db,
            Guid userId)
    {
        var preference =
            await db.NotificationPreferences
                .FirstOrDefaultAsync(
                    x =>
                        x.UserId == userId);

        if (preference != null)
        {
            return preference;
        }

        preference =
            new NotificationPreference
            {
                UserId =
                    userId,

                PushEnabled =
                    true,

                UpdatedAt =
                    DateTime.UtcNow
            };

        db.NotificationPreferences.Add(
            preference);

        await db.SaveChangesAsync();

        return preference;
    }

    private static async Task<Profile?>
        GetCurrentUserAsync(
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

public record DeviceTokenInput(
    string Token,
    string? Platform = "android");

public record NotificationPreferenceInput(
    bool PushEnabled);
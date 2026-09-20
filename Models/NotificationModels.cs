using System;
using System.Collections.Generic;

namespace OnSiteApi.Models;

public class DeviceToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public string Platform { get; set; } = "android";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } =
        DateTime.UtcNow;

    public Profile? User { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Data { get; set; } = "{}";

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public Profile? User { get; set; }
}

public class NotificationPreference
{
    public Guid UserId { get; set; }

    public bool PushEnabled { get; set; } = true;

    public DateTime UpdatedAt { get; set; } =
        DateTime.UtcNow;

    public Profile? User { get; set; }
}
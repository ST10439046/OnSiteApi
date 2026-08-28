using System;
using System.Collections.Generic;

namespace OnSiteApi.Models;

public class Profile
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Password { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<SiteForeman> SiteForemen { get; set; } = new List<SiteForeman>();
    public ICollection<SiteUpdate> SiteUpdates { get; set; } = new List<SiteUpdate>();
    public ICollection<TruckLog> TruckLogs { get; set; } = new List<TruckLog>();
}

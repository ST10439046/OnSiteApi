using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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

    // Passwords are managed by Supabase Auth.
    // This property is retained only for compatibility
    // with the existing database schema.
    [JsonIgnore]
    public string? Password { get; set; }

    // Navigation properties
    public ICollection<SiteForeman> SiteForemen { get; set; } = new List<SiteForeman>();

    public ICollection<SiteUpdate> SiteUpdates { get; set; } = new List<SiteUpdate>();

   
}
using System;
using System.Collections.Generic;

namespace OnSiteApi.Models;

public class Site
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<SiteForeman> SiteForemen { get; set; } = new List<SiteForeman>();
    public ICollection<SiteUpdate> SiteUpdates { get; set; } = new List<SiteUpdate>();
}

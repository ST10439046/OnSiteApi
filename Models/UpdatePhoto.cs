using System;

namespace OnSiteApi.Models;

public class UpdatePhoto
{
    public Guid Id { get; set; }
    public Guid UpdateId { get; set; }
    public SiteUpdate? SiteUpdate { get; set; }

    public string StoragePath { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System;
using System.Collections.Generic;

namespace OnSiteApi.Models;

public class SiteUpdate
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }

    public Guid ForemanId { get; set; }
    public Profile? Foreman { get; set; }

    public DateOnly UpdateDate { get; set; }
    public int ForecastedLabor { get; set; }
    public int Bricklayers { get; set; }
    public int Plasterers { get; set; }
    public int Pavers { get; set; }
    public int ActualLabor { get; set; }
    public string? StaffNames { get; set; }
    public string? PowerTools { get; set; }
    public string? PlantMachines { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UpdatePhoto> UpdatePhotos { get; set; } = new List<UpdatePhoto>();
}

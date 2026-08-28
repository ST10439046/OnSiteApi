using System;

namespace OnSiteApi.Models;

public class TruckLog
{
    public Guid Id { get; set; }
    
    public Guid DriverId { get; set; }
    public Profile? Driver { get; set; }

    public string DriverName { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public string TruckSize { get; set; } = string.Empty;
    public string LoadType { get; set; } = string.Empty;
    public string ArrivalTime { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string LeavingTime { get; set; } = string.Empty;
    public decimal? DieselLitres { get; set; }
    public string? DieselLocation { get; set; }
    public decimal? MileageBefore { get; set; }
    public decimal? MileageAfter { get; set; }
    public DateOnly LogDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

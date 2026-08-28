using System;

namespace OnSiteApi.Models;

public class SiteForeman
{
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }

    public Guid ForemanId { get; set; }
    public Profile? Foreman { get; set; }
}

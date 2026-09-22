using Microsoft.EntityFrameworkCore;
using OnSiteApi.Models;

namespace OnSiteApi.Data;

public class OnSiteDbContext : DbContext
{
    
    public OnSiteDbContext(
        DbContextOptions<OnSiteDbContext> options)
        : base(options)
    {
    }

    public DbSet<Profile> Profiles =>
        Set<Profile>();

    public DbSet<Site> Sites =>
        Set<Site>();

    public DbSet<SiteForeman> SiteForemen =>
        Set<SiteForeman>();

    public DbSet<SiteUpdate> SiteUpdates =>
        Set<SiteUpdate>();

    public DbSet<UpdatePhoto> UpdatePhotos =>
        Set<UpdatePhoto>();



    public DbSet<DeviceToken> DeviceTokens =>
        Set<DeviceToken>();

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    public DbSet<NotificationPreference>
        NotificationPreferences =>
        Set<NotificationPreference>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(
            modelBuilder);

        modelBuilder.Entity<Profile>(
            entity =>
            {
                entity.ToTable(
                    "profiles",
                    "public");

                entity.HasKey(
                    e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.FullName)
                    .HasColumnName("full_name")
                    .IsRequired();

                entity.Property(e => e.Role)
                    .HasColumnName("role")
                    .HasConversion(
                        v =>
                            v.ToString()
                                .ToLower()
                                .Replace(
                                    "truckdriver",
                                    "truck_driver"),

                        v =>
                            v == "truck_driver"
                                ? UserRole.TruckDriver
                                : Enum.Parse<UserRole>(
                                    v,
                                    true)
                    )
                    .IsRequired();

                entity.Property(e => e.Phone)
                    .HasColumnName("phone");

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .IsRequired();

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");

                entity.Property(e => e.Password)
                    .HasColumnName("password")
                    .IsRequired(false);
            });

        modelBuilder.Entity<Site>(
            entity =>
            {
                entity.ToTable(
                    "sites",
                    "public");

                entity.HasKey(
                    e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql(
                        "gen_random_uuid()");

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .IsRequired();

                entity.Property(e => e.Address)
                    .HasColumnName("address")
                    .IsRequired();

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");
            });

        modelBuilder.Entity<SiteForeman>(
            entity =>
            {
                entity.ToTable(
                    "site_foremen",
                    "public");

                entity.HasKey(
                    e =>
                        new
                        {
                            e.SiteId,
                            e.ForemanId
                        });

                entity.Property(e => e.SiteId)
                    .HasColumnName("site_id");

                entity.Property(e => e.ForemanId)
                    .HasColumnName("foreman_id");

                entity.HasOne(d => d.Site)
                    .WithMany(
                        p =>
                            p.SiteForemen)
                    .HasForeignKey(
                        d =>
                            d.SiteId)
                    .OnDelete(
                        DeleteBehavior.Cascade);

                entity.HasOne(d => d.Foreman)
                    .WithMany(
                        p =>
                            p.SiteForemen)
                    .HasForeignKey(
                        d =>
                            d.ForemanId)
                    .OnDelete(
                        DeleteBehavior.Cascade);
            });

        modelBuilder.Entity<SiteUpdate>(
            entity =>
            {
                entity.ToTable(
                    "site_updates",
                    "public");

                entity.HasKey(
                    e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql(
                        "gen_random_uuid()");

                entity.Property(e => e.SiteId)
                    .HasColumnName("site_id");

                entity.Property(e => e.ForemanId)
                    .HasColumnName("foreman_id");

                entity.Property(e => e.UpdateDate)
                    .HasColumnName("update_date")
                    .IsRequired();

                entity.Property(
                    e =>
                        e.ForecastedLabor)
                    .HasColumnName(
                        "forecasted_labor")
                    .HasDefaultValue(0);

                entity.Property(
                    e =>
                        e.Bricklayers)
                    .HasColumnName(
                        "bricklayers")
                    .HasDefaultValue(0);

                entity.Property(
                    e =>
                        e.Plasterers)
                    .HasColumnName(
                        "plasterers")
                    .HasDefaultValue(0);

                entity.Property(
                    e =>
                        e.Pavers)
                    .HasColumnName(
                        "pavers")
                    .HasDefaultValue(0);

                entity.Property(
                    e =>
                        e.ActualLabor)
                    .HasColumnName(
                        "actual_labor")
                    .HasDefaultValue(0);

                entity.Property(
                    e =>
                        e.StaffNames)
                    .HasColumnName(
                        "staff_names")
                    .HasColumnType("text");

                entity.Property(
                    e =>
                        e.PowerTools)
                    .HasColumnName(
                        "power_tools")
                    .HasColumnType("text");

                entity.Property(
                    e =>
                        e.PlantMachines)
                    .HasColumnName(
                        "plant_machines")
                    .HasColumnType("text");

                entity.Property(
                    e =>
                        e.Notes)
                    .HasColumnName(
                        "notes")
                    .HasColumnType("text");

                entity.Property(
                    e =>
                        e.CreatedAt)
                    .HasColumnName(
                        "created_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");

                entity.HasOne(
                    d =>
                        d.Site)
                    .WithMany(
                        p =>
                            p.SiteUpdates)
                    .HasForeignKey(
                        d =>
                            d.SiteId)
                    .OnDelete(
                        DeleteBehavior.Cascade);

                entity.HasOne(
                    d =>
                        d.Foreman)
                    .WithMany(
                        p =>
                            p.SiteUpdates)
                    .HasForeignKey(
                        d =>
                            d.ForemanId)
                    .OnDelete(
                        DeleteBehavior.Cascade);
            });

        modelBuilder.Entity<UpdatePhoto>(
            entity =>
            {
                entity.ToTable(
                    "update_photos",
                    "public");

                entity.HasKey(
                    e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql(
                        "gen_random_uuid()");

                entity.Property(e => e.UpdateId)
                    .HasColumnName("update_id");

                entity.Property(
                    e =>
                        e.PhotoData)
                    .HasColumnName("photo_data")
                    .HasColumnType("text")
                    .IsRequired();

                entity.Property(
                    e =>
                        e.Caption)
                    .HasColumnName("caption");

                entity.Property(
                    e =>
                        e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");

                entity.HasOne(
                    d =>
                        d.SiteUpdate)
                    .WithMany(
                        p =>
                            p.UpdatePhotos)
                    .HasForeignKey(
                        d =>
                            d.UpdateId)
                    .OnDelete(
                        DeleteBehavior.Cascade);
            });

       
        // =====================================================
        // DEVICE TOKENS
        // =====================================================

        modelBuilder.Entity<DeviceToken>(
            entity =>
            {
                entity.ToTable(
                    "device_tokens",
                    "public");

                entity.HasKey(
                    e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql(
                        "gen_random_uuid()");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .IsRequired();

                entity.Property(e => e.Token)
                    .HasColumnName("token")
                    .IsRequired();

                entity.Property(e => e.Platform)
                    .HasColumnName("platform")
                    .HasDefaultValue("android");

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");

                entity.HasIndex(
                    e => e.Token)
                    .IsUnique();

                entity.HasOne(
                    e => e.User)
                    .WithMany()
                    .HasForeignKey(
                        e => e.UserId)
                    .OnDelete(
                        DeleteBehavior.Cascade);
            });

        // =====================================================
        // NOTIFICATIONS
        // =====================================================

        modelBuilder.Entity<Notification>(
            entity =>
            {
                entity.ToTable(
                    "notifications",
                    "public");

                entity.HasKey(
                    e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql(
                        "gen_random_uuid()");

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .IsRequired();

                entity.Property(e => e.Type)
                    .HasColumnName("type")
                    .IsRequired();

                entity.Property(e => e.Title)
                    .HasColumnName("title")
                    .IsRequired();

                entity.Property(e => e.Message)
                    .HasColumnName("message")
                    .IsRequired();

                entity.Property(e => e.Data)
                    .HasColumnName("data")
                    .HasColumnType("jsonb")
                    .IsRequired();

                entity.Property(e => e.IsRead)
                    .HasColumnName("is_read")
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");

                entity.HasIndex(
                    e =>
                        new
                        {
                            e.UserId,
                            e.CreatedAt
                        });

                entity.HasOne(
                    e => e.User)
                    .WithMany()
                    .HasForeignKey(
                        e => e.UserId)
                    .OnDelete(
                        DeleteBehavior.Cascade);
            });

        // =====================================================
        // NOTIFICATION PREFERENCES
        // =====================================================

        modelBuilder.Entity<NotificationPreference>(
            entity =>
            {
                entity.ToTable(
                    "notification_preferences",
                    "public");

                entity.HasKey(
                    e => e.UserId);

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id");

                entity.Property(e => e.PushEnabled)
                    .HasColumnName("push_enabled")
                    .HasDefaultValue(true);

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql(
                        "timezone('utc'::text, now())");

                entity.HasOne(
                    e => e.User)
                    .WithMany()
                    .HasForeignKey(
                        e => e.UserId)
                    .OnDelete(
                        DeleteBehavior.Cascade);
            });
    }
}
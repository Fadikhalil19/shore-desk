using Desk.Core.Incidents;
using Microsoft.EntityFrameworkCore;

namespace Desk.Core.Data;

public class DeskDbContext(DbContextOptions<DeskDbContext> options) : DbContext(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<LocationArea> LocationAreas => Set<LocationArea>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<DepartmentAccess> DepartmentAccess => Set<DepartmentAccess>();
    public DbSet<UserAccess> UserAccess => Set<UserAccess>();
    public DbSet<ActivityLog> ActivityLog => Set<ActivityLog>();
    public DbSet<ListItem> ListItems => Set<ListItem>();
    public DbSet<IncidentReport> Incidents => Set<IncidentReport>();
    public DbSet<IncidentAttachment> IncidentAttachments => Set<IncidentAttachment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Enums are stored as readable text, not numbers.
        b.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        b.Properties<string>().HaveMaxLength(400);
        b.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Hotel>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(10);
            e.HasIndex(x => x.Code).IsUnique();
        });
        m.Entity<Department>().HasIndex(x => x.Name).IsUnique();

        m.Entity<LocationArea>(e =>
        {
            e.HasIndex(x => new { x.HotelId, x.Name }).IsUnique();
            e.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<Location>(e =>
        {
            e.HasIndex(x => new { x.AreaId, x.Name }).IsUnique();
            e.HasOne(x => x.Area).WithMany(a => a.Locations).HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.EntraObjectId).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<DepartmentAccess>(e =>
        {
            e.Property(x => x.Module).HasMaxLength(40);
            e.HasIndex(x => new { x.DepartmentId, x.Module }).IsUnique();
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.LimitHotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.LimitDepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<UserAccess>(e =>
        {
            e.Property(x => x.Module).HasMaxLength(40);
            e.HasIndex(x => new { x.AppUserId, x.Module }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.LimitHotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.LimitDepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<ActivityLog>(e =>
        {
            e.Property(x => x.Details).HasMaxLength(-1);
            e.Property(x => x.Module).HasMaxLength(40);
            e.Property(x => x.Action).HasMaxLength(60);
            e.Property(x => x.EntityType).HasMaxLength(60);
            e.Property(x => x.EntityId).HasMaxLength(60);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.Utc);
            e.HasIndex(x => new { x.UserId, x.Utc });
            e.HasIndex(x => new { x.Module, x.EntityType, x.EntityId });
        });

        m.Entity<ListItem>(e =>
        {
            e.Property(x => x.ListKey).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => new { x.ListKey, x.Name }).IsUnique();
        });

        m.Entity<IncidentReport>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Kind).HasMaxLength(40);
            e.Property(x => x.OtherType).HasMaxLength(200);
            e.Property(x => x.Heading).HasMaxLength(200);
            e.Property(x => x.LocationDetail).HasMaxLength(200);
            e.Property(x => x.Room).HasMaxLength(40);
            e.Property(x => x.Narrative).HasMaxLength(-1);
            e.Property(x => x.FollowUp).HasMaxLength(-1);
            e.Property(x => x.DetailsJson).HasMaxLength(-1);
            e.Property(x => x.ReviewNote).HasMaxLength(1000);
            e.Property(x => x.VoidReason).HasMaxLength(1000);
            e.HasIndex(x => x.OccurredLocal);
            e.HasIndex(x => new { x.Status, x.SupervisorId });
            e.HasIndex(x => x.WrittenById);
            e.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Type).WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.WrittenBy).WithMany().HasForeignKey(x => x.WrittenById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Attachments).WithOne().HasForeignKey(a => a.IncidentId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<IncidentAttachment>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.StoredName).HasMaxLength(80);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Caption).HasMaxLength(400);
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RemovedById).OnDelete(DeleteBehavior.Restrict);
        });

        SeedData.Apply(m);
        SeedLists.Apply(m);
    }
}

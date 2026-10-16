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

        SeedData.Apply(m);
    }
}

using Desk.Core.Events;
using Desk.Core.Handover;
using Desk.Core.Incidents;
using Desk.Core.Keys;
using Desk.Core.LostFound;
using Desk.Core.OnDuty;
using Desk.Core.Packages;
using Desk.Core.Patrol;
using Desk.Core.Spaces;
using Desk.Core.Visitors;
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
    public DbSet<SecurityEvent> Events => Set<SecurityEvent>();
    public DbSet<EventReminder> EventReminders => Set<EventReminder>();
    public DbSet<HandoverNote> HandoverNotes => Set<HandoverNote>();
    public DbSet<HandoverRead> HandoverReads => Set<HandoverRead>();
    public DbSet<DutyAssignment> DutyAssignments => Set<DutyAssignment>();
    public DbSet<CallOut> CallOuts => Set<CallOut>();
    public DbSet<HotelKey> Keys => Set<HotelKey>();
    public DbSet<KeyCheckout> KeyCheckouts => Set<KeyCheckout>();
    public DbSet<KeyStatusChange> KeyStatusChanges => Set<KeyStatusChange>();
    public DbSet<KeyCount> KeyCounts => Set<KeyCount>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<ScheduledVisit> ScheduledVisits => Set<ScheduledVisit>();
    public DbSet<Space> Spaces => Set<Space>();
    public DbSet<SpaceAssignment> SpaceAssignments => Set<SpaceAssignment>();
    public DbSet<FoundItem> FoundItems => Set<FoundItem>();
    public DbSet<LostReport> LostReports => Set<LostReport>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<PatrolRoute> PatrolRoutes => Set<PatrolRoute>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<PatrolRound> PatrolRounds => Set<PatrolRound>();
    public DbSet<CheckpointScan> CheckpointScans => Set<CheckpointScan>();

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
            e.Property(x => x.LegacyRef).HasMaxLength(60);
            e.HasIndex(x => x.LegacyRef);
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

        m.Entity<SecurityEvent>(e =>
        {
            e.Property(x => x.Detail).HasMaxLength(200);
            e.Property(x => x.LocationDetail).HasMaxLength(200);
            e.Property(x => x.Room).HasMaxLength(40);
            e.Property(x => x.RequestedByType).HasMaxLength(40);
            e.Property(x => x.RequestedByName).HasMaxLength(200);
            e.Property(x => x.RelatesToType).HasMaxLength(40);
            e.Property(x => x.RelatesToName).HasMaxLength(200);
            e.Property(x => x.RelatesToRoom).HasMaxLength(40);
            e.Property(x => x.Narrative).HasMaxLength(-1);
            e.Property(x => x.VoidReason).HasMaxLength(1000);
            e.Property(x => x.LegacyRef).HasMaxLength(60);
            e.HasIndex(x => x.LegacyRef);
            e.HasIndex(x => x.StartedLocal);
            e.HasIndex(x => new { x.CompletedLocal, x.HotelId });
            e.HasOne(x => x.Hotel).WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.RequestedByDepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.AssignedDepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<EventReminder>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.ClosedUtc, x.DueLocal });
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.AssignedDepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.LastDoneById).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<HandoverNote>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(4000);
            e.HasIndex(x => new { x.HotelId, x.ShiftDate, x.Shift });
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RemovedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<HandoverRead>(e =>
        {
            e.HasIndex(x => new { x.HotelId, x.ShiftDate, x.Shift, x.UserId }).IsUnique();
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<DutyAssignment>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Radio).HasMaxLength(40);
            e.Property(x => x.Post).HasMaxLength(100);
            e.HasIndex(x => new { x.HotelId, x.ShiftDate, x.Shift });
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<CallOut>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Reason).HasMaxLength(40);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.CoveredBy).HasMaxLength(200);
            e.HasIndex(x => new { x.HotelId, x.ShiftDate, x.Shift });
            e.HasIndex(x => x.UserId);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<HotelKey>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Barcode).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(400);
            e.HasIndex(x => new { x.HotelId, x.Name }).IsUnique();
            e.HasIndex(x => new { x.HotelId, x.Barcode }).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<KeyCheckout>().WithMany().HasForeignKey(x => x.CurrentCheckoutId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<KeyCheckout>(e =>
        {
            e.Property(x => x.ToType).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Company).HasMaxLength(200);
            e.Property(x => x.OutNotes).HasMaxLength(1000);
            e.Property(x => x.InNotes).HasMaxLength(1000);
            e.Property(x => x.OutSignature).HasMaxLength(-1);
            e.Property(x => x.InSignature).HasMaxLength(-1);
            e.HasIndex(x => new { x.HotelId, x.OutLocal });
            e.HasIndex(x => x.KeyId);
            e.HasOne(x => x.Key).WithMany().HasForeignKey(x => x.KeyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OutById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<KeyStatusChange>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex(x => x.KeyId);
            e.HasOne<HotelKey>().WithMany().HasForeignKey(x => x.KeyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<KeyCount>(e =>
        {
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.HotelId, x.ShiftDate, x.Shift });
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CountedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.KeyCountId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<KeyCountLine>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(400);
            e.HasOne<HotelKey>().WithMany().HasForeignKey(x => x.KeyId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<Visit>(e =>
        {
            e.Property(x => x.VisitorName).HasMaxLength(200);
            e.Property(x => x.Company).HasMaxLength(200);
            e.Property(x => x.VisitorType).HasMaxLength(40);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.AreaDetail).HasMaxLength(200);
            e.Property(x => x.HostName).HasMaxLength(200);
            e.Property(x => x.Purpose).HasMaxLength(400);
            e.Property(x => x.BadgeColor).HasMaxLength(40);
            e.Property(x => x.BadgeNumber).HasMaxLength(40);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.CompanionsJson).HasMaxLength(4000);
            e.Property(x => x.PhotoStoredName).HasMaxLength(80);
            e.Property(x => x.VoidReason).HasMaxLength(1000);
            e.HasIndex(x => new { x.HotelId, x.OutLocal });
            e.HasIndex(x => x.InLocal);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.HostUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CheckedOutById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ScheduledVisit>(e =>
        {
            e.Property(x => x.VisitorName).HasMaxLength(200);
            e.Property(x => x.Company).HasMaxLength(200);
            e.Property(x => x.VisitorType).HasMaxLength(40);
            e.Property(x => x.HostName).HasMaxLength(200);
            e.Property(x => x.Purpose).HasMaxLength(400);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.HotelId, x.ExpectedLocal });
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.HostUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<Space>(e =>
        {
            e.Property(x => x.Section).HasMaxLength(40);
            e.Property(x => x.Number).HasMaxLength(40);
            e.Property(x => x.LockCode).HasMaxLength(40);
            e.Property(x => x.LockSerial).HasMaxLength(60);
            e.Property(x => x.Notes).HasMaxLength(400);
            e.HasIndex(x => new { x.HotelId, x.Kind, x.Section, x.Number }).IsUnique();
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SpaceAssignment>().WithMany().HasForeignKey(x => x.CurrentAssignmentId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<SpaceAssignment>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            foreach (var p in new[] { "VehicleMake", "VehicleModel", "VehicleColor", "PlateState" }) e.Property(p).HasMaxLength(60);
            e.Property(x => x.VehicleYear).HasMaxLength(10);
            e.Property(x => x.Plate).HasMaxLength(20);
            e.Property(x => x.StickerNumber).HasMaxLength(40);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => x.SpaceId);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Space).WithMany().HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ReleasedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<FoundItem>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Detail).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Brand).HasMaxLength(100);
            e.Property(x => x.ModelSerial).HasMaxLength(100);
            e.Property(x => x.LocationDetail).HasMaxLength(200);
            e.Property(x => x.Room).HasMaxLength(40);
            e.Property(x => x.FoundByType).HasMaxLength(40);
            e.Property(x => x.FoundByName).HasMaxLength(200);
            e.Property(x => x.StorageLocation).HasMaxLength(100);
            e.Property(x => x.OwnerName).HasMaxLength(200);
            e.Property(x => x.OwnerContact).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.PhotoStoredName).HasMaxLength(80);
            e.Property(x => x.ReturnedToName).HasMaxLength(200);
            e.Property(x => x.ReturnSignature).HasMaxLength(-1);
            e.Property(x => x.DisposalMethod).HasMaxLength(40);
            e.Property(x => x.CloseNote).HasMaxLength(1000);
            e.HasIndex(x => new { x.HotelId, x.Status, x.PurgeDate });
            e.HasIndex(x => x.FoundLocal);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ListItem>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ListItem>().WithMany().HasForeignKey(x => x.ColorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.FoundByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(x => x.FoundByDepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ClosedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<LostReport>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.OwnerName).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Room).HasMaxLength(40);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Brand).HasMaxLength(100);
            e.Property(x => x.LostWhere).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => new { x.HotelId, x.Status });
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ListItem>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ListItem>().WithMany().HasForeignKey(x => x.ColorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<FoundItem>().WithMany().HasForeignKey(x => x.FoundItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<Shipment>(e =>
        {
            e.Property(x => x.RecipientName).HasMaxLength(200);
            e.Property(x => x.Street).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.State).HasMaxLength(60);
            e.Property(x => x.Zip).HasMaxLength(20);
            e.Property(x => x.Country).HasMaxLength(60);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Carrier).HasMaxLength(40);
            e.Property(x => x.TrackingNumber).HasMaxLength(60);
            e.Property(x => x.PaymentNote).HasMaxLength(400);
            e.HasIndex(x => x.FoundItemId);
            e.HasOne(x => x.FoundItem).WithMany().HasForeignKey(x => x.FoundItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<Package>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(20);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Carrier).HasMaxLength(40);
            e.Property(x => x.PackageType).HasMaxLength(40);
            e.Property(x => x.TrackingNumber).HasMaxLength(80);
            e.Property(x => x.Weight).HasMaxLength(40);
            e.Property(x => x.PersonType).HasMaxLength(40);
            e.Property(x => x.PersonName).HasMaxLength(200);
            e.Property(x => x.Room).HasMaxLength(40);
            e.Property(x => x.Company).HasMaxLength(200);
            e.Property(x => x.GroupName).HasMaxLength(200);
            e.Property(x => x.OtherParty).HasMaxLength(400);
            e.Property(x => x.StorageLocation).HasMaxLength(100);
            e.Property(x => x.NotifiedHow).HasMaxLength(40);
            e.Property(x => x.ClosedToName).HasMaxLength(200);
            e.Property(x => x.Signature).HasMaxLength(-1);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => new { x.HotelId, x.Status });
            e.HasIndex(x => x.TrackingNumber);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.PersonUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.NotifiedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ClosedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<Checkpoint>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Instructions).HasMaxLength(400);
            e.HasIndex(x => new { x.HotelId, x.Name }).IsUnique();
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<PatrolRoute>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => new { x.HotelId, x.Name }).IsUnique();
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Stops).WithOne().HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<RouteStop>(e =>
        {
            e.HasOne(x => x.Checkpoint).WithMany().HasForeignKey(x => x.CheckpointId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<PatrolRound>(e =>
        {
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.HotelId, x.StartedLocal });
            e.HasIndex(x => new { x.OfficerId, x.Status });
            e.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OfficerId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<CheckpointScan>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex(x => new { x.HotelId, x.ScannedLocal });
            e.HasIndex(x => x.RoundId);
            e.HasOne(x => x.Checkpoint).WithMany().HasForeignKey(x => x.CheckpointId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PatrolRound>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OfficerId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<IncidentReport>().HasIndex(x => new { x.ForHandover, x.OccurredLocal });
        m.Entity<SecurityEvent>().HasIndex(x => new { x.ForHandover, x.StartedLocal });

        SeedData.Apply(m);
        SeedLists.Apply(m);
    }
}

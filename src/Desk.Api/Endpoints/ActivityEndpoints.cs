using Desk.Api.Services;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Endpoints;

// Admin-only reports under Settings: the activity log (every view, edit, download,
// sign-in), who signed in, and whether the app is healthy.
public static class ActivityEndpoints
{
    public record Entry(long Id, DateTime Utc, int? UserId, string UserName, string Action, string? Module,
        string? EntityType, string? EntityId, string? Details, string? IpAddress);
    public record Page(int Total, List<Entry> Items);

    public record SignInPerson(int Id, string Name, string? Email, string? Role, DateTime? FirstSignInUtc,
        DateTime? LastSignInUtc, int SignIns, string? LastIp);
    public record SignInReport(List<SignInPerson> People, Page Events);

    public static void MapActivityEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization(Policies.Admin);

        g.MapGet("/activity", async (string? q, string? action, string? module, int? userId, DateOnly? from, DateOnly? to,
            int? page, DeskDbContext db, CancellationToken ct) =>
        {
            var query = db.ActivityLog.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(a => a.UserName.Contains(q) || (a.Details != null && a.Details.Contains(q)));
            if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
            if (!string.IsNullOrWhiteSpace(module)) query = query.Where(a => a.Module == module);
            if (userId is int uid) query = query.Where(a => a.UserId == uid);
            query = InRange(query, from, to);
            return await PageOf(query, page ?? 1, ct);
        });

        g.MapGet("/activity/actions", async (DeskDbContext db, CancellationToken ct) =>
            await db.ActivityLog.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct));

        // Sign-ins: one row per person, plus the history (refused attempts included).
        g.MapGet("/reports/signins", async (string? action, DateOnly? from, DateOnly? to, int? page,
            DeskDbContext db, CancellationToken ct) =>
        {
            var people = await db.Users.Where(u => u.RemovedUtc == null).Select(u => new
            {
                u.Id, u.DisplayName, u.Email, u.FirstSignInUtc,
                Count = db.ActivityLog.Count(a => a.UserId == u.Id && a.Action == "SignIn"),
                Last = db.ActivityLog.Where(a => a.UserId == u.Id && a.Action == "SignIn").OrderByDescending(a => a.Id)
                    .Select(a => new { a.Utc, a.Details, a.IpAddress }).FirstOrDefault(),
            }).ToListAsync(ct);
            var rows = people
                .Select(p => new SignInPerson(p.Id, p.DisplayName, p.Email, p.Last?.Details, p.FirstSignInUtc, p.Last?.Utc, p.Count, p.Last?.IpAddress))
                .OrderByDescending(p => p.LastSignInUtc ?? DateTime.MinValue).ToList();

            var q = db.ActivityLog.Where(a => a.Action == "SignIn" || a.Action == "Denied");
            if (action is "SignIn" or "Denied") q = q.Where(a => a.Action == action);
            return new SignInReport(rows, await PageOf(InRange(q, from, to), page ?? 1, ct));
        });

        g.MapGet("/reports/health", async (DeskDbContext db, IConfiguration config, CancellationToken ct) =>
            await HealthReport.BuildAsync(db, config, ct));
    }

    // Dates are days in Pacific time, as people read them.
    static IQueryable<ActivityLog> InRange(IQueryable<ActivityLog> q, DateOnly? from, DateOnly? to)
    {
        if (from is DateOnly f) { var fu = PacificToUtc(f); q = q.Where(a => a.Utc >= fu); }
        if (to is DateOnly t) { var tu = PacificToUtc(t.AddDays(1)); q = q.Where(a => a.Utc < tu); }
        return q;
    }

    static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    static DateTime PacificToUtc(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), Pacific);

    public static async Task<Page> PageOf(IQueryable<ActivityLog> q, int page, CancellationToken ct)
    {
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.Id).Skip((Math.Max(page, 1) - 1) * 100).Take(100)
            .Select(a => new Entry(a.Id, a.Utc, a.UserId, a.UserName, a.Action, a.Module, a.EntityType, a.EntityId, a.Details, a.IpAddress))
            .ToListAsync(ct);
        return new Page(total, items);
    }
}

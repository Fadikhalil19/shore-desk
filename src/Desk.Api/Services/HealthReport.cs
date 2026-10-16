using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text.Json;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Services;

// Settings > App health: a list of checks, each Ok / Warning / Problem / Info, in plain words.
// Every check is independent: one that cannot be read shows as a warning, never breaks the page.
public static class HealthReport
{
    public record Check(string Area, string Name, string Status, string Message, string? Detail = null);
    public record Report(DateTime CheckedUtc, List<Check> Checks);

    public class Expiry
    {
        public string Name { get; set; } = "";
        public DateOnly Date { get; set; }
        public string? Note { get; set; }
    }

    const string Ok = "Ok", Warn = "Warning", Bad = "Problem", Info = "Info";

    public static async Task<Report> BuildAsync(DeskDbContext db, IConfiguration config, CancellationToken ct)
    {
        var h = config.GetSection("Health");
        var checks = new List<Check>();
        async Task Run(string area, string name, Func<Task<Check>> check)
        {
            try { checks.Add(await check()); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                checks.Add(new Check(area, name, Warn, "Could not check this.", ex.Message));
            }
        }
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, Pacific));

        // --- Background jobs ---
        await Run("Background jobs", "Scheduler", () =>
        {
            var file = h["HeartbeatFile"] ?? @"C:\DeskData\status\scheduler-heartbeat.txt";
            if (!File.Exists(file)) return Task.FromResult(new Check("Background jobs", "Scheduler", Bad, "The scheduler has never reported in."));
            var age = now - File.GetLastWriteTimeUtc(file);
            var line = File.ReadAllText(file).Trim();
            var status = line.StartsWith("error") ? Bad : age > TimeSpan.FromHours(2) ? Bad : age > TimeSpan.FromMinutes(30) ? Warn : Ok;
            var msg = line.StartsWith("error") ? "Last run failed." : $"Last ran {Ago(age)}.";
            return Task.FromResult(new Check("Background jobs", "Scheduler", status, msg, line));
        });

        foreach (var svc in h.GetSection("Services").Get<string[]>() ?? [])
            await Run("Background jobs", $"Service {svc}", () =>
            {
                using var sc = new ServiceController(svc);
                var st = sc.Status;
                return Task.FromResult(new Check("Background jobs", $"Service {svc}", st == ServiceControllerStatus.Running ? Ok : Bad,
                    st == ServiceControllerStatus.Running ? "Running." : $"Not running ({st}).", ServiceNote(svc)));
            });

        await Run("Background jobs", "Who receives emails", () =>
        {
            var file = h["SchedulerSettingsFile"] ?? @"C:\DeskApp\scheduler\appsettings.json";
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var redirect = doc.RootElement.TryGetProperty("Scheduler", out var s) && s.TryGetProperty("RedirectAllTo", out var r) ? r.GetString() : null;
            return Task.FromResult(string.IsNullOrWhiteSpace(redirect)
                ? new Check("Background jobs", "Who receives emails", Info, "Live: emails go to the real people.")
                : new Check("Background jobs", "Who receives emails", Info, $"Not live yet: every email goes to {redirect} only."));
        });

        // --- Backups ---
        await Run("Backups", "Nightly backup", () =>
        {
            var okFile = h["BackupLastOkFile"] ?? @"C:\DeskData\status\backup-last-ok.txt";
            var logFile = h["BackupLogFile"] ?? @"C:\DeskData\status\backup-log.txt";
            var lines = File.Exists(logFile) ? File.ReadAllLines(logFile) : [];
            var failures14 = lines.Count(l => l.Contains("FAILED") && DateTime.TryParse(l.Length >= 10 ? l[..10] : "", out var d) && d >= DateTime.Today.AddDays(-14));
            if (!File.Exists(okFile)) return Task.FromResult(new Check("Backups", "Nightly backup", Bad, "No successful backup found."));
            var age = now - File.GetLastWriteTimeUtc(okFile);
            var lastFailed = lines.LastOrDefault(l => l.Contains("FAILED") || l.Contains("finished OK"))?.Contains("FAILED") == true;
            var status = lastFailed || age > TimeSpan.FromHours(26) ? Bad : failures14 > 0 ? Warn : Ok;
            var msg = $"Last good backup {Ago(age)}."
                      + (lastFailed ? " The most recent backup FAILED." : "")
                      + (failures14 > 0 ? $" {failures14} failed in the last 14 days." : "");
            return Task.FromResult(new Check("Backups", "Nightly backup", status, msg,
                string.Join("\n", lines.Where(l => l.Contains("FAILED") || l.Contains("finished OK")).TakeLast(6))));
        });

        // --- Security ---
        await Run("Security", "Refused sign-ins", async () =>
        {
            var since = now.AddDays(-7);
            var denied = await db.ActivityLog.Where(a => a.Action == "Denied" && a.Utc >= since).Select(a => a.UserName).ToListAsync(ct);
            return new Check("Security", "Refused sign-ins", denied.Count == 0 ? Ok : Warn,
                denied.Count == 0 ? "No refused sign-ins in the last 7 days."
                    : $"{denied.Count} refused sign-ins in the last 7 days. See Settings > Sign-ins.",
                string.Join("; ", denied.Distinct()));
        });

        await Run("Security", "People without a department", async () =>
        {
            var n = await db.Users.CountAsync(u => u.BlockedUtc == null && u.DepartmentId == null, ct);
            return new Check("Security", "People without a department", n == 0 ? Ok : Info,
                n == 0 ? "Everyone has a department." : $"{n} people have no department, so they get no department default access.");
        });

        // --- Server ---
        await Run("Server", "Disk space", () =>
        {
            var d = new DriveInfo("C");
            var freeGb = d.AvailableFreeSpace / 1024d / 1024 / 1024;
            return Task.FromResult(new Check("Server", "Disk space", freeGb < 5 ? Bad : freeGb < 20 ? Warn : Ok,
                $"{freeGb:0} GB free of {d.TotalSize / 1024d / 1024 / 1024:0} GB on C:."));
        });

        await Run("Server", "Database size", async () =>
        {
            // SQL Server Express stops accepting data at 10 GB per database.
            var mb = await db.Database.SqlQueryRaw<int>(
                "SELECT CAST(SUM(CAST(size AS bigint)) * 8 / 1024 AS int) AS Value FROM sys.database_files WHERE type = 0").SingleAsync(ct);
            return new Check("Server", "Database size", mb > 8000 ? Bad : mb > 6000 ? Warn : Ok,
                $"{mb:N0} MB of the 10,240 MB limit for SQL Server Express.");
        });

        await Run("Server", "Stored files", () =>
        {
            var root = config["App:StorageRoot"] ?? @"C:\DeskData\files";
            var files = Directory.Exists(root) ? new DirectoryInfo(root).EnumerateFiles("*", SearchOption.AllDirectories).ToList() : [];
            return Task.FromResult(new Check("Server", "Stored files", Info, $"{files.Count:N0} files, {files.Sum(f => f.Length) / 1024d / 1024:N0} MB."));
        });

        // --- Expiry dates ---
        await Run("Expiry dates", "Website certificate", () =>
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var subject = h["CertificateSubject"] ?? "desk.shorehotelapps.com";
            var cert = store.Certificates.Find(X509FindType.FindBySubjectName, subject, false)
                .OrderByDescending(c => c.NotAfter).FirstOrDefault();
            if (cert is null) return Task.FromResult(new Check("Expiry dates", "Website certificate", Warn, $"Certificate {subject} not found."));
            return Task.FromResult(ExpiryCheck("Website certificate", DateOnly.FromDateTime(cert.NotAfter), today, "Self-signed certificate between Cloudflare and the app."));
        });
        foreach (var e in h.GetSection("Expiries").Get<List<Expiry>>() ?? [])
            checks.Add(ExpiryCheck(e.Name, e.Date, today, e.Note));

        return new Report(now, checks);
    }

    static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    static Check ExpiryCheck(string name, DateOnly date, DateOnly today, string? note)
    {
        var days = date.DayNumber - today.DayNumber;
        var status = days < 14 ? Bad : days < 60 ? Warn : Ok;
        var msg = days < 0 ? $"EXPIRED on {date:MMM d, yyyy}." : $"Expires {date:MMM d, yyyy} ({days:N0} days).";
        return new Check("Expiry dates", name, status, msg, note);
    }

    static string Ago(TimeSpan t) =>
        t.TotalMinutes < 2 ? "just now" :
        t.TotalHours < 2 ? $"{t.TotalMinutes:0} minutes ago" :
        t.TotalDays < 2 ? $"{t.TotalHours:0} hours ago" : $"{t.TotalDays:0} days ago";

    static string? ServiceNote(string svc) => svc switch
    {
        "ShoreDeskScheduler" => "Sends emails and reminders.",
        "cloudflared" => "Cloudflare Tunnel: makes the app reachable at desk.shorehotelapps.com (shared with the contract app).",
        "MSSQL$SQLEXPRESS" => "The database (shared with the contract app).",
        _ => null,
    };
}

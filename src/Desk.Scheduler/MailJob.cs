using System.Net;
using Desk.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Desk.Scheduler;

// Emails the scheduler sends. Phase 1: tell the admins the first time each person signs in.
// Later phases add reminders (keys out too long, visitors overstaying, missed patrols, ...).
public class MailJob(DeskDbContext db, IMailSender mail, IOptions<SchedulerOptions> options, ILogger<MailJob> log)
{
    SchedulerOptions O => options.Value;

    public async Task<int> FirstSignInMailAsync(CancellationToken ct)
    {
        if (O.AdminEmails.Length == 0) return 0;
        var sent = 0;
        var users = await db.Users.Include(u => u.Department).Include(u => u.Hotel)
            .Where(u => u.FirstSignInUtc != null && u.FirstSignInNotifiedUtc == null).ToListAsync(ct);
        foreach (var u in users)
        {
            var signIn = await db.ActivityLog.Where(a => a.UserId == u.Id && a.Action == "SignIn")
                .OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
            try
            {
                foreach (var to in O.AdminEmails)
                    await SendAsync(FirstSignInEmail(to, u, signIn), ct);
                u.FirstSignInNotifiedUtc = DateTime.UtcNow;
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "First sign-in email for user {Id} failed; will retry next run", u.Id);
            }
            await db.SaveChangesAsync(ct);
        }
        return sent;
    }

    Email FirstSignInEmail(string to, AppUser u, ActivityLog? signIn)
    {
        string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var when = signIn is null ? "" : TimeZoneInfo.ConvertTimeFromUtc(signIn.Utc, Pacific).ToString("MMM d, yyyy h:mm tt");
        var dept = u.Department?.Name ?? "not set yet";
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937">
            <p><b>{E(u.DisplayName)}</b> ({E(u.Email)}) signed in to Shore Desk for the first time{(when == "" ? "" : $" on {E(when)} (Pacific)")}.</p>
            <p>Role: {E(signIn?.Details)}<br>Department: {E(dept)}<br>From address: {E(signIn?.IpAddress)}</p>
            <p>{(u.DepartmentId is null ? "They see nothing yet until you set their department or access." : "They get their department's default access.")}
            <a href="{O.BaseUrl}/settings?tab=people&amp;person={u.Id}">Check their access</a></p>
            <p style="color:#6b7280">If you do not know this person, disable them in Settings &gt; People and take them out of the DESK groups in Entra.</p>
            </div>
            """;
        return new Email(to, $"[Shore Desk] First sign-in: {u.DisplayName}", html);
    }

    static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    // Every email leaves through here so the pre-live redirect cannot be bypassed.
    Task SendAsync(Email e, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(O.RedirectAllTo) || string.Equals(e.To, O.RedirectAllTo, StringComparison.OrdinalIgnoreCase)
            ? mail.SendAsync(e, ct)
            : mail.SendAsync(e with { To = O.RedirectAllTo, ReplyTo = null, Subject = $"[Not live - meant for {e.To}] {e.Subject}" }, ct);
}

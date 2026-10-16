namespace Desk.Scheduler;

public class SchedulerOptions
{
    public const string Section = "Scheduler";

    // Address used in email links.
    public string BaseUrl { get; set; } = "https://desk.shorehotelapps.com";

    // Told when someone signs in for the first time.
    public string[] AdminEmails { get; set; } = [];

    // "Outbox" writes each email as an HTML file (practice mode, nothing is sent).
    // "Graph" sends through Microsoft Graph from the shared mailbox (after the desk@ mailbox is set up).
    public string Mode { get; set; } = "Outbox";
    public string OutboxDir { get; set; } = @"C:\DeskData\outbox";

    // Graph mode settings (mailer app not created yet).
    public string FromMailbox { get; set; } = "desk@shorehotel.com";
    public string TenantId { get; set; } = "c7319400-c050-406d-b027-2eb54f8b6f90";
    public string MailerClientId { get; set; } = "";
    public string SecretsDir { get; set; } = @"C:\DeskData\secrets";

    // Before go-live every email goes to this address instead of the real recipient, with
    // "[Not live - meant for ...]" in the subject. Empty = send to the real people.
    // Fadi's rule: nothing reaches anyone else until he says the app is live.
    public string? RedirectAllTo { get; set; }

    public int IntervalMinutes { get; set; } = 5;

    // Written every run so a missing heartbeat can raise the alarm (Settings > App health).
    public string HeartbeatFile { get; set; } = @"C:\DeskData\status\scheduler-heartbeat.txt";
}

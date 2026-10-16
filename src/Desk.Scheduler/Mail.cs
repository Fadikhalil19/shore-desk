using System.Net;
using Microsoft.Extensions.Options;

namespace Desk.Scheduler;

// ReplyTo: replies reach a person instead of the shared mailbox.
public record Email(string To, string Subject, string HtmlBody, string? ReplyTo = null);

public interface IMailSender
{
    Task SendAsync(Email email, CancellationToken ct);
}

// Practice mode: each email becomes an HTML file in the outbox folder. Nothing is sent.
public class FileOutboxSender(IOptions<SchedulerOptions> options) : IMailSender
{
    public async Task SendAsync(Email email, CancellationToken ct)
    {
        var dir = options.Value.OutboxDir;
        Directory.CreateDirectory(dir);
        var safeTo = string.Concat(email.To.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{safeTo}.html");
        var html = $"<!-- To: {WebUtility.HtmlEncode(email.To)} -->\n<!-- Subject: {WebUtility.HtmlEncode(email.Subject)} -->\n{email.HtmlBody}";
        await File.WriteAllTextAsync(path, html, ct);
    }
}

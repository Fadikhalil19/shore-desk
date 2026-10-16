using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Desk.Scheduler;

// Sends email through Microsoft Graph as the shared mailbox. The Entra app
// ("Shore Desk Mailer", to be created with the desk@ mailbox) has Mail.Send, and an Exchange application access
// policy limits it to that one mailbox.
public class GraphMailSender : IMailSender
{
    readonly HttpClient _http;
    readonly TokenCredential _credential;
    readonly string _from;
    static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];

    public GraphMailSender(HttpClient http, IOptions<SchedulerOptions> options)
    {
        var o = options.Value;
        _http = http;
        _from = o.FromMailbox;
        var secret = File.ReadAllText(Path.Combine(o.SecretsDir, "mailer-client-secret.txt")).Trim();
        _credential = new ClientSecretCredential(o.TenantId, o.MailerClientId, secret);
    }

    public async Task SendAsync(Email email, CancellationToken ct)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(Scopes), ct);
        var body = new
        {
            message = new
            {
                subject = email.Subject,
                body = new { contentType = "HTML", content = email.HtmlBody },
                toRecipients = new[] { new { emailAddress = new { address = email.To } } },
                replyTo = email.ReplyTo is null ? Array.Empty<object>() : [new { emailAddress = new { address = email.ReplyTo } }],
            },
            saveToSentItems = true,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(_from)}/sendMail")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var detail = await res.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Graph sendMail failed ({(int)res.StatusCode}): {detail}");
        }
    }
}

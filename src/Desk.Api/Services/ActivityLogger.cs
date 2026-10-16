using System.Text.Json;
using Desk.Api.Auth;
using Desk.Core.Data;

namespace Desk.Api.Services;

// Every view, edit and download is written here (a CLAUDE.md rule).
public class ActivityLogger(DeskDbContext db, CurrentUser me, IHttpContextAccessor http)
{
    public async Task LogAsync(string action, string? module = null, string? entityType = null,
        string? entityId = null, object? details = null, CancellationToken ct = default)
    {
        var user = await me.GetUserAsync(ct);
        db.ActivityLog.Add(new ActivityLog
        {
            Utc = DateTime.UtcNow,
            UserId = user?.Id,
            UserName = user?.DisplayName ?? me.Name,
            Action = action,
            Module = module,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details),
            IpAddress = ClientIp(http.HttpContext)
        });
        await db.SaveChangesAsync(ct);
    }

    // Behind Cloudflare the real visitor address arrives in CF-Connecting-IP.
    public static string? ClientIp(HttpContext? ctx)
    {
        if (ctx is null) return null;
        var cf = ctx.Request.Headers["CF-Connecting-IP"].ToString();
        return string.IsNullOrWhiteSpace(cf) ? ctx.Connection.RemoteIpAddress?.ToString() : cf;
    }
}

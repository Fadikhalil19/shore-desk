using System.Security.Claims;
using Desk.Api.Services;
using Desk.Core.Data;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Auth;

// Runs once per Microsoft sign-in: refuse anyone who is not allowed, record the
// person, and stamp their role on the session cookie.
public static class SignInHandler
{
    public static async Task OnTokenValidatedAsync(TokenValidatedContext ctx, string tenantId, AppOptions options)
    {
        var principal = ctx.Principal!;
        var db = ctx.HttpContext.RequestServices.GetRequiredService<DeskDbContext>();
        var name = principal.FindFirst("name")?.Value ?? "unknown";
        var email = principal.FindFirst("preferred_username")?.Value;
        var ip = ActivityLogger.ClientIp(ctx.HttpContext);
        var now = DateTime.UtcNow;

        async Task Deny(int? userId, string reason)
        {
            db.ActivityLog.Add(new ActivityLog
            {
                Utc = now, UserId = userId, UserName = $"{name} <{email}>", Action = "Denied", Details = reason, IpAddress = ip
            });
            await db.SaveChangesAsync();
            ctx.Response.Redirect("/denied");
            ctx.HandleResponse();
        }

        var reason = SignInRules.DenyReason(principal, tenantId, options);
        if (reason is not null) { await Deny(null, reason); return; }
        if (SignInRules.ObjectId(principal) is not Guid oid) { await Deny(null, "Sign-in had no account ID."); return; }

        var user = await db.Users.FirstOrDefaultAsync(u => u.EntraObjectId == oid);
        if (user?.BlockedUtc is not null)
        {
            await Deny(user.Id, user.RemovedUtc is null ? "Disabled in the app by an admin" : "Removed from the app by an admin");
            return;
        }

        var role = SignInRules.RoleFromGroups(principal.FindAll("groups").Select(c => c.Value), options.Groups);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(SignInRules.RoleClaim, role.ToString()));
        // Drop the raw group list from the cookie; the role is all the app needs.
        foreach (var g in identity.FindAll("groups").ToList()) identity.RemoveClaim(g);

        if (user is null)
        {
            user = new AppUser { EntraObjectId = oid, DisplayName = name, Email = email, FirstSeenUtc = now, LastSeenUtc = now, FirstSignInUtc = now };
            db.Users.Add(user);
        }
        else
        {
            user.DisplayName = name;
            user.Email = email;
            user.LastSeenUtc = now;
            user.FirstSignInUtc ??= now;
        }
        await db.SaveChangesAsync();

        db.ActivityLog.Add(new ActivityLog
        {
            Utc = now, UserId = user.Id, UserName = name, Action = "SignIn", Details = role.ToString(), IpAddress = ip
        });
        await db.SaveChangesAsync();
    }
}

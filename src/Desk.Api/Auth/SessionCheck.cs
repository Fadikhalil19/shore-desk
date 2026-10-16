using Desk.Core.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Desk.Api.Auth;

// On every request with a sign-in cookie: if an admin has disabled or removed the
// person since they signed in, throw the cookie away so they are out immediately.
public static class SessionCheck
{
    public static async Task EndIfBlockedAsync(CookieValidatePrincipalContext ctx)
    {
        if (SignInRules.ObjectId(ctx.Principal) is not Guid oid) return;
        var db = ctx.HttpContext.RequestServices.GetRequiredService<DeskDbContext>();
        if (await db.Users.AnyAsync(u => u.EntraObjectId == oid && u.BlockedUtc != null))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

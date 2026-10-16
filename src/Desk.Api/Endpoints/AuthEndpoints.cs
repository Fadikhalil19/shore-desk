using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Desk.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // returnUrl lets a patrol tag link (/p/...) come back to itself after sign-in.
        // Only local paths are accepted, never another website.
        app.MapGet("/auth/login", (string? returnUrl) =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = SafeLocal(returnUrl) },
                [OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous();

        app.MapGet("/auth/logout", () =>
            Results.SignOut(new AuthenticationProperties { RedirectUri = "/signed-out" },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
            .AllowAnonymous();

        app.MapGet("/signed-out", () => Page("Signed out",
                "You are signed out of Shore Desk.", "/auth/login", "Sign in again"))
            .AllowAnonymous();

        app.MapGet("/denied", () => Page("No access",
                "Your account is not set up for Shore Desk. If you need access, ask Fadi Khalil.",
                "/auth/logout", "Sign out"))
            .AllowAnonymous();
    }

    public static string SafeLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") ? url : "/";

    static IResult Page(string title, string message, string link, string linkText) => Results.Content(
        $$"""
        <!doctype html><html lang="en"><head><meta charset="utf-8"><title>{{WebUtility.HtmlEncode(title)}} - Shore Desk</title>
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{font-family:system-ui,sans-serif;max-width:32rem;margin:15vh auto;padding:0 1rem;color:#1f2937}
        a{color:#0b5cad}</style></head>
        <body><h1>{{title}}</h1><p>{{message}}</p><p><a href="{{link}}">{{linkText}}</a></p></body></html>
        """, "text/html");
}

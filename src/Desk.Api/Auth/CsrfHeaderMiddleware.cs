namespace Desk.Api.Auth;

// Cross-site request forgery guard for the cookie-based API: every write request
// must carry "X-CSRF: 1". Another website cannot add a custom header to a request
// aimed at us without a CORS permission, which this app never grants.
public class CsrfHeaderMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var isWrite = !(HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method)
                        || HttpMethods.IsOptions(ctx.Request.Method));
        if (isWrite && ctx.Request.Path.StartsWithSegments("/api") && ctx.Request.Headers["X-CSRF"] != "1")
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Missing X-CSRF header.");
            return;
        }
        await next(ctx);
    }
}

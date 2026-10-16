using Desk.Core.Access;

namespace Desk.Api.Auth;

// Puts a module check on an endpoint: .RequireModule(Modules.Keys, AccessLevel.Edit).
// Someone below the level gets 403. Limits by hotel or department inside the
// module are checked by the endpoint itself (EffectiveAccess.Allows).
public static class ModuleAccessFilter
{
    public static TBuilder RequireModule<TBuilder>(this TBuilder builder, string module, AccessLevel level)
        where TBuilder : IEndpointConventionBuilder
    {
        if (Modules.Find(module) is null) throw new ArgumentException($"Unknown module {module}", nameof(module));
        return builder.AddEndpointFilter(async (ctx, next) =>
        {
            var me = ctx.HttpContext.RequestServices.GetRequiredService<CurrentUser>();
            var access = await me.AccessToAsync(module, ctx.HttpContext.RequestAborted);
            return access.Level >= level ? await next(ctx) : Results.StatusCode(StatusCodes.Status403Forbidden);
        });
    }
}

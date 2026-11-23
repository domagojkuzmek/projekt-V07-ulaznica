using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Okviri.Api.Sigurnost;

/// <summary>401 / 403 u JSON formatu kao u T2: <c>{ "greska": "..." }</c>.</summary>
public sealed class JsonOdgovorAutorizacije : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _zadano = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await context.ChallengeAsync(); // 401 + WWW-Authenticate: Bearer
            await context.Response.WriteAsJsonAsync(new { greska = "Potrebna je prijava." });
            return;
        }
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { greska = "Nemate ovlasti za ovu radnju." });
            return;
        }
        await _zadano.HandleAsync(next, context, policy, authorizeResult);
    }
}

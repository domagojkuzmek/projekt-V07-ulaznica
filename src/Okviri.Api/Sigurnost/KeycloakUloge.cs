using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Okviri.Api.Sigurnost;

/// <summary>
/// Keycloak realm uloge su u ugniježđenom claimu <c>realm_access: { "roles": [...] }</c>;
/// pretvaramo ih u claimove tipa <see cref="TipUloge"/> da bi radili [Authorize(Roles)], RequireRole i IsInRole.
/// </summary>
public sealed class KeycloakUloge : IClaimsTransformation
{
    public const string TipUloge = "uloga";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identitet
            || identitet.HasClaim(c => c.Type == TipUloge))
        {
            return Task.FromResult(principal);
        }
        var realmAccess = identitet.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return Task.FromResult(principal);
        }
        using var json = JsonDocument.Parse(realmAccess);
        if (!json.RootElement.TryGetProperty("roles", out var uloge) || uloge.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(principal);
        }
        var kopija = principal.Clone();
        var noviIdentitet = (ClaimsIdentity)kopija.Identity!;
        foreach (var uloga in uloge.EnumerateArray())
        {
            if (uloga.GetString() is { Length: > 0 } naziv)
            {
                noviIdentitet.AddClaim(new Claim(TipUloge, naziv));
            }
        }
        return Task.FromResult(kopija);
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Okviri.Api.Sigurnost;

namespace Okviri.Tests;

/// <summary>
/// Testna shema umjesto JwtBearer: zaglavlja X-Test-Korisnik / X-Test-Uloge postaju claimovi
/// kakve daje Keycloak (preferred_username, realm_access). Keycloak nije potreban, a NAŠA
/// transformacija uloga (KeycloakUloge), politike i handleri rade kao u produkciji.
/// </summary>
public sealed class TestnaShema(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Naziv = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Korisnik", out var korisnik))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var uloge = Request.Headers["X-Test-Uloge"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        var identitet = new ClaimsIdentity(
            [
                new Claim("preferred_username", korisnik.ToString()),
                new Claim("realm_access", JsonSerializer.Serialize(new { roles = uloge }), "JSON"),
            ],
            Naziv, "preferred_username", KeycloakUloge.TipUloge);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identitet), Naziv)));
    }
}

public class OkviriFactory : WebApplicationFactory<Program>
{
    protected virtual int PokusajaPrijave => 5;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Obavijesti:ApiKljuc", "testni-kljuc-0123456789");
        builder.UseSetting("Prijava:PokusajaUMinuti", PokusajaPrijave.ToString());
        builder.ConfigureTestServices(s =>
        {
            s.AddAuthentication(TestnaShema.Naziv)
                .AddScheme<AuthenticationSchemeOptions, TestnaShema>(TestnaShema.Naziv, _ => { });
            s.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestnaShema.Naziv;
                o.DefaultAuthenticateScheme = TestnaShema.Naziv;
                o.DefaultChallengeScheme = TestnaShema.Naziv;
            });
        });
    }

    public HttpRequestMessage Zahtjev(HttpMethod metoda, string url, string? korisnik = null, params string[] uloge)
    {
        var z = new HttpRequestMessage(metoda, url);
        if (korisnik is not null)
        {
            z.Headers.Add("X-Test-Korisnik", korisnik);
            z.Headers.Add("X-Test-Uloge", string.Join(',', uloge));
        }
        return z;
    }
}

/// <summary>Zasebna aplikacija s manjim limitom (svježi rate limiter).</summary>
public sealed class OkviriFactoryStrogiLimit : OkviriFactory
{
    protected override int PokusajaPrijave => 3;
}

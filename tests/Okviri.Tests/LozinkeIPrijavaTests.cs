using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Okviri.Api.Prijava;
using Xunit;

namespace Okviri.Tests;

public sealed class LozinkeIPrijavaTests(OkviriFactory app) : IClassFixture<OkviriFactory>
{
    private readonly HasherSMigracijom _hasher = new();
    private readonly LokalniKorisnik _k = new("test");

    [Fact]
    public void NoviHashImaSolIProlaziProvjeru()
    {
        var prvi = _hasher.HashPassword(_k, "Lozinka123!");
        var drugi = _hasher.HashPassword(_k, "Lozinka123!");
        Assert.NotEqual(prvi, drugi); // sol: ista lozinka, različit hash
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_k, prvi, "Lozinka123!"));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(_k, prvi, "Kriva"));
    }

    [Fact]
    public void StariSha256HashTraziZamjenu()
    {
        const string stari = "78bc5813d03977dbf07326fae76c9a3e1504f4c20291cf523b6a95d148a904b9";
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _hasher.VerifyHashedPassword(_k, stari, "Marko12345!"));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(_k, stari, "Kriva"));
    }

    [Fact]
    public async Task PrijavaMigriraStariHashIPogresnaDaje401()
    {
        var klijent = app.CreateClient();
        var ok = await klijent.PostAsJsonAsync("/api/auth/login", new { username = "marko", password = "Marko12345!" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var hash = app.Services.GetRequiredService<LokalniKorisnici>().Hash("marko");
        Assert.NotNull(hash);
        Assert.NotEqual(64, hash.Length); // više nije SHA-256 hex, nego PasswordHasher v3 format

        var krivo = await klijent.PostAsJsonAsync("/api/auth/login", new { username = "ana", password = "pogresna" });
        Assert.Equal(HttpStatusCode.Unauthorized, krivo.StatusCode);
        var nepostoji = await klijent.PostAsJsonAsync("/api/auth/login", new { username = "nepostoji", password = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, nepostoji.StatusCode);
    }
}

public sealed class OgranicenjePrijaveTests(OkviriFactoryStrogiLimit app) : IClassFixture<OkviriFactoryStrogiLimit>
{
    [Fact]
    public async Task NakonTriPokusajaSlijedi429()
    {
        var klijent = app.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            var o = await klijent.PostAsJsonAsync("/api/auth/login", new { username = "ana", password = "pogadam" });
            Assert.Equal(HttpStatusCode.Unauthorized, o.StatusCode);
        }
        var odbijeno = await klijent.PostAsJsonAsync("/api/auth/login", new { username = "ana", password = "pogadam" });
        Assert.Equal(HttpStatusCode.TooManyRequests, odbijeno.StatusCode);
        Assert.True(odbijeno.Headers.Contains("Retry-After"));
    }
}

public sealed class PokretanjeTests
{
    private sealed class BezTajne : OkviriFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Obavijesti:ApiKljuc", "");
        }
    }

    [Fact]
    public void BezApiKljucaAplikacijaSeNePokrece()
    {
        using var app = new BezTajne();
        var greska = Record.Exception(() => app.CreateClient());
        Assert.NotNull(greska);
        Assert.Contains("ApiKljuc", greska.ToString());
    }
}

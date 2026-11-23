using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Okviri.Api.Rezervacije;
using Xunit;

namespace Okviri.Tests;

public sealed class SigurnostTests : IClassFixture<OkviriFactory>
{
    private readonly OkviriFactory _app;
    private readonly HttpClient _klijent;

    public SigurnostTests(OkviriFactory app)
    {
        _app = app;
        _klijent = app.CreateClient();
        app.Services.GetRequiredService<RezervacijeRepozitorij>().VratiPocetnePodatke();
    }

    private Task<HttpResponseMessage> Posalji(HttpMethod m, string url, string? korisnik = null, params string[] uloge) =>
        _klijent.SendAsync(_app.Zahtjev(m, url, korisnik, uloge));

    private static async Task<JsonElement> Json(HttpResponseMessage o) =>
        JsonDocument.Parse(await o.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task HealthJeJavan()
    {
        var o = await Posalji(HttpMethod.Get, "/api/health");
        Assert.Equal(HttpStatusCode.OK, o.StatusCode);
    }

    [Fact]
    public async Task BezPrijaveJe401UJsonFormatu()
    {
        var o = await Posalji(HttpMethod.Get, "/api/rezervacije/moje");
        Assert.Equal(HttpStatusCode.Unauthorized, o.StatusCode);
        Assert.Equal("Potrebna je prijava.", (await Json(o)).GetProperty("greska").GetString());
    }

    [Fact]
    public async Task MojeVracaSamoRezervacijePrijavljenogKorisnika()
    {
        var o = await Posalji(HttpMethod.Get, "/api/rezervacije/moje", "ana", "KORISNIK");
        var popis = await o.Content.ReadFromJsonAsync<List<Rezervacija>>(JsonSerializerOptions.Web);
        Assert.NotNull(popis);
        Assert.Equal(new long[] { 1, 3 }, popis.Select(r => r.Id).ToArray());
        Assert.All(popis, r => Assert.Equal("ana", r.Pacijent));
    }

    [Fact]
    public async Task TudaRezervacijaSeNeBriseI404()
    {
        var o = await Posalji(HttpMethod.Delete, "/api/rezervacije/1", "marko", "KORISNIK");
        Assert.Equal(HttpStatusCode.NotFound, o.StatusCode);
        Assert.NotNull(_app.Services.GetRequiredService<RezervacijeRepozitorij>().Pronadi(1));
    }

    [Fact]
    public async Task NepostojecaIsto404KaoTuda()
    {
        var o = await Posalji(HttpMethod.Delete, "/api/rezervacije/999", "marko", "KORISNIK");
        Assert.Equal(HttpStatusCode.NotFound, o.StatusCode);
    }

    [Fact]
    public async Task VlasnikBriseSvoju()
    {
        var o = await Posalji(HttpMethod.Delete, "/api/rezervacije/3", "ana", "KORISNIK");
        Assert.Equal(HttpStatusCode.NoContent, o.StatusCode);
        Assert.Null(_app.Services.GetRequiredService<RezervacijeRepozitorij>().Pronadi(3));
    }

    [Fact]
    public async Task AdminBriseBiloKoju()
    {
        var o = await Posalji(HttpMethod.Delete, "/api/rezervacije/2", "admin", "ADMIN");
        Assert.Equal(HttpStatusCode.NoContent, o.StatusCode);
    }

    [Fact]
    public async Task AdminskiPopisSamoZaAdmina()
    {
        var ana = await Posalji(HttpMethod.Get, "/api/admin/rezervacije", "ana", "KORISNIK");
        Assert.Equal(HttpStatusCode.Forbidden, ana.StatusCode);
        Assert.Equal("Nemate ovlasti za ovu radnju.", (await Json(ana)).GetProperty("greska").GetString());
        var admin = await Posalji(HttpMethod.Get, "/api/admin/rezervacije", "admin", "ADMIN");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.Equal(3, (await Json(admin)).GetArrayLength());
    }

    [Fact]
    public async Task SigurnosnaZaglavlja()
    {
        var o = await Posalji(HttpMethod.Get, "/api/health");
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", o.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", o.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", o.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", o.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task CorsSamoZaDopusteniOrigin()
    {
        foreach (var (origin, dopusten) in new[] { ("http://localhost:5173", true), ("https://zlo.example", false) })
        {
            var z = new HttpRequestMessage(HttpMethod.Options, "/api/rezervacije/moje");
            z.Headers.Add("Origin", origin);
            z.Headers.Add("Access-Control-Request-Method", "GET");
            var o = await _klijent.SendAsync(z);
            o.Headers.TryGetValues("Access-Control-Allow-Origin", out var acao);
            if (dopusten)
            {
                Assert.Equal(origin, acao?.Single());
            }
            else
            {
                Assert.Null(acao);
            }
        }
    }
}

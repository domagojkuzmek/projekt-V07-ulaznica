using System.Net;
using System.Net.Http.Json;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Funkcionalno;

[Trait("Category", "Funkcionalno")]
public sealed class PrijavaTests(UlaznicaFactory factory) : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task HealthVracaUp()
    {
        var odgovor = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        Assert.Equal("UP", (await odgovor.CitajJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task IspravnaPrijavaVracaTokenIUlogu()
    {
        var odgovor = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "organizator", password = "Ovlasteni12345!" });

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        var json = await odgovor.CitajJsonAsync();
        Assert.False(string.IsNullOrEmpty(json.GetProperty("token").GetString()));
        Assert.Equal("organizator", json.GetProperty("username").GetString());
        Assert.Equal("ORGANIZATOR", json.GetProperty("uloga").GetString());
    }

    [Fact]
    public async Task PogresnaLozinkaVraca401()
    {
        var odgovor = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "ana", password = "pogresna" });

        Assert.Equal(HttpStatusCode.Unauthorized, odgovor.StatusCode);
        Assert.Equal("Neispravno korisničko ime ili lozinka.", (await odgovor.CitajJsonAsync()).GetProperty("greska").GetString());
    }

    [Fact]
    public async Task JaVracaPodatkePrijavljenogKorisnika()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var json = await (await klijent.GetAsync("/api/ja")).CitajJsonAsync();

        Assert.Equal("ana", json.GetProperty("username").GetString());
        Assert.Equal("Ana", json.GetProperty("ime").GetString());
        Assert.Equal("Anić", json.GetProperty("prezime").GetString());
        Assert.Equal("ana@primjer.hr", json.GetProperty("email").GetString());
        Assert.Equal("KORISNIK", json.GetProperty("uloga").GetString());
    }

    [Fact]
    public async Task RegistracijaIPrijavaNovogKorisnika()
    {
        var klijent = factory.CreateClient();
        await klijent.RegistrirajAsync("novi.kupac", "Lozinka123!");

        var token = await klijent.PrijaviAsync("novi.kupac", "Lozinka123!");

        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task ZauzetoKorisnickoImeIKratkaLozinkaSeOdbijaju()
    {
        var klijent = factory.CreateClient();

        var zauzeto = await klijent.PostAsJsonAsync("/api/auth/register", new { username = "marko", password = "Lozinka123!" });
        var kratka = await klijent.PostAsJsonAsync("/api/auth/register", new { username = "kratka.lozinka", password = "abc" });

        Assert.Equal(HttpStatusCode.Conflict, zauzeto.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, kratka.StatusCode);
    }
}

using System.Net;
using System.Text;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Funkcionalno;

[Trait("Category", "Funkcionalno")]
public sealed class RacuniTests(UlaznicaFactory factory) : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task MojiRacuniSadrzeSamoVlastiteRacune()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var racuni = await (await klijent.GetAsync("/api/racuni/moji")).CitajJsonAsync();

        var racun = Assert.Single(racuni.EnumerateArray());
        Assert.Equal("racun-ana-2026-0001.txt", racun.GetProperty("datoteka").GetString());
    }

    [Fact]
    public async Task PreuzimanjeVlastiteDatoteke()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var odgovor = await klijent.GetAsync("/api/racuni/preuzmi?datoteka=racun-ana-2026-0001.txt");

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        var sadrzaj = await odgovor.Content.ReadAsStringAsync();
        Assert.Contains("2026-0001", sadrzaj);
        Assert.Contains("Ukupno za platiti: 70.00 EUR", sadrzaj);
    }

    [Fact]
    public async Task RacunUJsonFormatu()
    {
        var klijent = await factory.KlijentZaAsync("marko", "Marko12345!");

        var odgovor = await klijent.GetAsync("/api/racuni/2?format=json");

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        var json = await odgovor.CitajJsonAsync();
        Assert.Equal(2, json.GetProperty("id").GetInt64());
        Assert.Contains("Hamlet", json.GetProperty("sadrzaj").GetString());
    }

    [Fact]
    public async Task RacunUPdfFormatu()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var odgovor = await klijent.GetAsync("/api/racuni/1?format=pdf");

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        Assert.Equal("application/pdf", odgovor.Content.Headers.ContentType?.MediaType);
        var bajtovi = await odgovor.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bajtovi, 0, 5));
        Assert.Contains("Ukupno za platiti", Encoding.ASCII.GetString(bajtovi));
    }

    [Fact]
    public async Task AdministratorVidiSveRacune()
    {
        var klijent = await factory.KlijentZaAsync("admin", "Admin12345!");

        var odgovor = await klijent.GetAsync("/api/racuni/1?format=json");

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
    }
}

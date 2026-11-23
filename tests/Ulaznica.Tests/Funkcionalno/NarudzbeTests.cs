using System.Net;
using System.Net.Http.Json;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Funkcionalno;

[Trait("Category", "Funkcionalno")]
public sealed class NarudzbeTests(UlaznicaFactory factory) : IClassFixture<UlaznicaFactory>
{
    private static async Task<long> NaruciAsync(HttpClient klijent, long dogadjajId, int kolicina, string imeNaUlaznici)
    {
        var odgovor = await klijent.PostAsJsonAsync("/api/narudzbe", new { dogadjajId, kolicina, imeNaUlaznici, oib = "48291736504" });
        Assert.Equal(HttpStatusCode.Created, odgovor.StatusCode);
        return (await odgovor.CitajJsonAsync()).GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task MojeNarudzbeSadrzePocetnuNarudzbu()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var moje = await (await klijent.GetAsync("/api/narudzbe/moje")).CitajJsonAsync();

        var prva = moje.EnumerateArray().Single(n => n.GetProperty("id").GetInt64() == 1);
        Assert.Equal("Ana Anić", prva.GetProperty("imeNaUlaznici").GetString());
        Assert.Equal(2, prva.GetProperty("kolicina").GetInt32());
        Assert.Equal("*******6504", prva.GetProperty("oib").GetString());
        Assert.DoesNotContain(moje.EnumerateArray(), n => n.GetProperty("id").GetInt64() == 2);
    }

    [Fact]
    public async Task NovaNarudzbaVidljivaJeIPovecavaProdano()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var id = await NaruciAsync(klijent, 3, 2, "Ana Anić");

        var moje = await (await klijent.GetAsync("/api/narudzbe/moje")).CitajJsonAsync();
        var nova = moje.EnumerateArray().Single(n => n.GetProperty("id").GetInt64() == id);
        Assert.Equal("Jazz večer", nova.GetProperty("dogadjaj").GetString());
        Assert.Equal(36m, nova.GetProperty("ukupno").GetDecimal());
        var dogadjaji = await (await klijent.GetAsync("/api/dogadjaji")).CitajJsonAsync();
        var jazz = dogadjaji.EnumerateArray().Single(d => d.GetProperty("id").GetInt64() == 3);
        Assert.Equal(122, jazz.GetProperty("prodano").GetInt32());
    }

    [Fact]
    public async Task RasprodanDogadjajVraca409()
    {
        var klijent = await factory.KlijentZaAsync("marko", "Marko12345!");

        var odgovor = await klijent.PostAsJsonAsync("/api/narudzbe", new { dogadjajId = 6, kolicina = 1, imeNaUlaznici = "Marko Marić", oib = "73019485267" });

        Assert.Equal(HttpStatusCode.Conflict, odgovor.StatusCode);
    }

    [Fact]
    public async Task NeispravnaKolicinaVraca400()
    {
        var klijent = await factory.KlijentZaAsync("marko", "Marko12345!");

        var odgovor = await klijent.PostAsJsonAsync("/api/narudzbe", new { dogadjajId = 4, kolicina = 5, imeNaUlaznici = "Marko Marić", oib = "73019485267" });

        Assert.Equal(HttpStatusCode.BadRequest, odgovor.StatusCode);
    }

    [Fact]
    public async Task VlastitaNarudzbaSeMozeStornirati()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");
        var id = await NaruciAsync(klijent, 4, 1, "Ana Anić");

        var storno = await klijent.DeleteAsync($"/api/narudzbe/{id}");

        Assert.Equal(HttpStatusCode.NoContent, storno.StatusCode);
        var moje = await (await klijent.GetAsync("/api/narudzbe/moje")).CitajJsonAsync();
        Assert.DoesNotContain(moje.EnumerateArray(), n => n.GetProperty("id").GetInt64() == id);
    }

    [Fact]
    public async Task IspisUlazniceSadrziImeNaUlaznici()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");
        var id = await NaruciAsync(klijent, 7, 1, "Mala Ana Anic");

        var odgovor = await klijent.GetAsync($"/ispis/narudzbe/{id}");

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        Assert.Equal("text/html", odgovor.Content.Headers.ContentType?.MediaType);
        var html = await odgovor.Content.ReadAsStringAsync();
        Assert.Contains("Mala Ana Anic", html);
        Assert.Contains("Kod ulaznice", html);
    }
}

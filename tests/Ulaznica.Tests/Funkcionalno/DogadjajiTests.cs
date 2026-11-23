using System.Net;
using System.Text.Json;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Funkcionalno;

[Trait("Category", "Funkcionalno")]
public sealed class DogadjajiTests(UlaznicaFactory factory) : IClassFixture<UlaznicaFactory>
{
    private static List<string> Nazivi(JsonElement json) =>
        json.EnumerateArray().Select(d => d.GetProperty("naziv").GetString()!).ToList();

    [Fact]
    public async Task ZadanoSortiranjePoDatumuUzlazno()
    {
        var odgovor = await factory.CreateClient().GetAsync("/api/dogadjaji");

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        var nazivi = Nazivi(await odgovor.CitajJsonAsync());
        Assert.Equal(8, nazivi.Count);
        Assert.Equal("Hamlet", nazivi[0]);
        Assert.Equal("Novogodišnji koncert", nazivi[^1]);
    }

    [Fact]
    public async Task SortiranjePoCijeni()
    {
        var klijent = factory.CreateClient();

        var uzlazno = await (await klijent.GetAsync("/api/dogadjaji?sort=cijena&smjer=asc")).CitajJsonAsync();
        var silazno = await (await klijent.GetAsync("/api/dogadjaji?sort=cijena&smjer=desc")).CitajJsonAsync();

        Assert.Equal("Dječja predstava: Crvenkapica", uzlazno[0].GetProperty("naziv").GetString());
        Assert.Equal(8m, uzlazno[0].GetProperty("cijena").GetDecimal());
        Assert.Equal("Novogodišnji koncert", silazno[0].GetProperty("naziv").GetString());
    }

    [Fact]
    public async Task SortiranjePoNazivuIMjestu()
    {
        var klijent = factory.CreateClient();

        var poNazivu = Nazivi(await (await klijent.GetAsync("/api/dogadjaji?sort=naziv&smjer=asc")).CitajJsonAsync());
        var poMjestu = await (await klijent.GetAsync("/api/dogadjaji?sort=mjesto&smjer=asc")).CitajJsonAsync();

        Assert.Equal("Dječja predstava: Crvenkapica", poNazivu[0]);
        Assert.Equal("Zagrebačka filharmonija – zadnja ulaznica", poNazivu[^1]);
        Assert.Equal("Arena Zagreb", poMjestu[0].GetProperty("mjesto").GetString());
    }

    [Fact]
    public async Task PopisPrikazujeSlobodnaMjesta()
    {
        var dogadjaji = await (await factory.CreateClient().GetAsync("/api/dogadjaji")).CitajJsonAsync();

        var rasprodano = dogadjaji.EnumerateArray().Single(d => d.GetProperty("id").GetInt64() == 6);
        Assert.Equal(0, rasprodano.GetProperty("slobodno").GetInt32());
        var koncert = dogadjaji.EnumerateArray().Single(d => d.GetProperty("id").GetInt64() == 1);
        Assert.Equal(600, koncert.GetProperty("slobodno").GetInt32());
    }
}

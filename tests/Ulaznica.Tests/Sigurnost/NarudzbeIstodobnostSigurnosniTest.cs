using System.Net;
using System.Net.Http.Json;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

[Trait("Category", "Sigurnost")]
public sealed class NarudzbeIstodobnostSigurnosniTest(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task SamJedanKorisnikMozeKupitiZadnjuUlaznicuIstodobno()
    {
        using var ana = await factory.KlijentZaAsync("ana", "Ana12345!");
        using var marko = await factory.KlijentZaAsync("marko", "Marko12345!");
        using var admin = await factory.KlijentZaAsync("admin", "Admin12345!");
        var klijenti = new[] { ana, marko, admin };

        var odgovori = await Task.WhenAll(klijenti.Select(klijent =>
            klijent.PostAsJsonAsync("/api/narudzbe", new
            {
                dogadjajId = 5,
                kolicina = 1,
                imeNaUlaznici = "Kupac",
                oib = "48291736504",
            })));

        Assert.Single(odgovori, odgovor => odgovor.StatusCode == HttpStatusCode.Created);
        Assert.Equal(2, odgovori.Count(odgovor => odgovor.StatusCode == HttpStatusCode.Conflict));

        using var citac = factory.CreateClient();
        var dogadjaji = await (await citac.GetAsync("/api/dogadjaji")).CitajJsonAsync();
        var zadnjiDogadjaj = dogadjaji.EnumerateArray()
            .Single(dogadjaj => dogadjaj.GetProperty("id").GetInt64() == 5);
        Assert.Equal(100, zadnjiDogadjaj.GetProperty("prodano").GetInt32());

        foreach (var odgovor in odgovori)
        {
            odgovor.Dispose();
        }
    }
}

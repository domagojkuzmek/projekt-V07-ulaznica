using System.Net;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

/// <summary>
/// Sigurnosni test: provjerava da korisnik ne može stornirati
/// narudžbu koja pripada drugom korisniku; odgovor ne otkriva postoji li zapis.
/// </summary>
[Trait("Category", "Sigurnost")]
public sealed class VlasnistvoNarudzbeSigurnosniTest(
    UlaznicaFactory factory) : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task KorisnikNeMozeStorniratiTuduNarudzbu()
    {
        // Arrange – prijavljeni korisnik koji nije vlasnik narudžbe
        using var klijent = await factory.KlijentZaAsync("marko", "Marko12345!");

        // Act
        var odgovor = await klijent.DeleteAsync("/api/narudzbe/1");

        // Assert – tuđi zapis izgleda kao da ne postoji i ostaje netaknut
        Assert.Equal(HttpStatusCode.NotFound, odgovor.StatusCode);

        using var ana = await factory.KlijentZaAsync("ana", "Ana12345!");
        var narudzbeAne = await (await ana.GetAsync("/api/narudzbe/moje")).CitajJsonAsync();
        Assert.Contains(narudzbeAne.EnumerateArray(), narudzba => narudzba.GetProperty("id").GetInt64() == 1);
    }
}

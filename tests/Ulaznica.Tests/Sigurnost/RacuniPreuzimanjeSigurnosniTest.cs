using System.Net;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

[Trait("Category", "Sigurnost")]
public sealed class RacuniPreuzimanjeSigurnosniTest(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task KorisnikNeMozePreuzetiTudiRacunPoNazivu()
    {
        using var marko = await factory.KlijentZaAsync("marko", "Marko12345!");

        var odgovor = await marko.GetAsync("/api/racuni/preuzmi?datoteka=racun-ana-2026-0001.txt");

        Assert.Equal(HttpStatusCode.NotFound, odgovor.StatusCode);
    }

    [Fact]
    public async Task NazivKojiSadrziKorisnickoImeNeDopustaProlazIzvanMapeDokumenata()
    {
        const string nazivDatoteke = "privatno-marko.txt";
        const string sadrzajDatoteke = "sadrzaj izvan mape racuna";
        var putanjaDatoteke = Path.Combine(factory.DirektorijPoste, nazivDatoteke);
        await File.WriteAllTextAsync(putanjaDatoteke, sadrzajDatoteke);

        try
        {
            using var marko = await factory.KlijentZaAsync("marko", "Marko12345!");
            var parametar = Uri.EscapeDataString($"../posta/{nazivDatoteke}");

            var odgovor = await marko.GetAsync($"/api/racuni/preuzmi?datoteka={parametar}");

            Assert.Equal(HttpStatusCode.NotFound, odgovor.StatusCode);
            Assert.DoesNotContain(sadrzajDatoteke, await odgovor.Content.ReadAsStringAsync());
        }
        finally
        {
            File.Delete(putanjaDatoteke);
        }
    }
}


using System.Net;
using System.Net.Http.Json;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

[Trait("Category", "Sigurnost")]
public sealed class IspisUlazniceTest(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task IspisUlazniceNeIzvrsavaZlonamjerniHtml()
    {
        // Arrange
        var klijent = await factory.KlijentZaAsync(
            "marko", "Marko12345!");

        const string zlonamjerniTekst =
            "<script>alert('XSS')</script>";

        var narudzba = new
        {
            DogadjajId = 1,
            Oib = "12345678903",
            Kolicina = 1,
            ImeNaUlaznici = zlonamjerniTekst
        };

        // Act – kreiranje narudžbe
        var odgovorNarudzbe = await klijent.PostAsJsonAsync(
            "/api/narudzbe", narudzba);

        Assert.Equal(
            HttpStatusCode.Created,
            odgovorNarudzbe.StatusCode);

        var json = await odgovorNarudzbe.CitajJsonAsync();
        var idNarudzbe = json.GetProperty("id").GetInt64();

        // Act – ispis ulaznice
        var odgovor = await klijent.GetAsync(
            $"/ispis/narudzbe/{idNarudzbe}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        Assert.Contains("default-src 'none'", Assert.Single(odgovor.Headers.GetValues("Content-Security-Policy")));

        var html = await odgovor.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            zlonamjerniTekst,
            html,
            StringComparison.OrdinalIgnoreCase);

        var ocekivaniTekst =
    System.Text.Encodings.Web.HtmlEncoder.Default.Encode(
        zlonamjerniTekst);

        Assert.Contains(ocekivaniTekst, html);
        Assert.DoesNotContain(zlonamjerniTekst, html);
    }
}

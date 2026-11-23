using System.Net;
using System.Text;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

[Trait("Category", "Sigurnost")]
public sealed class PostavkeYamlSigurnosniTest(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task UgnijezdeniYamlObjektSeOdbija()
    {
        using var klijent = await factory.KlijentZaAsync("marko", "Marko12345!");
        const string yaml = """
            podsjetnik:
              neocekivano: true
            satiPrije: 24
            kanal: email
            """;

        var odgovor = await klijent.PostAsync(
            "/api/postavke", new StringContent(yaml, Encoding.UTF8, "application/yaml"));

        Assert.Equal(HttpStatusCode.BadRequest, odgovor.StatusCode);
    }

    [Fact]
    public async Task DupliciraniYamlKljucSeOdbija()
    {
        using var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");
        const string yaml = """
            podsjetnik: true
            podsjetnik: false
            """;

        var odgovor = await klijent.PostAsync(
            "/api/postavke", new StringContent(yaml, Encoding.UTF8, "application/yaml"));

        Assert.Equal(HttpStatusCode.BadRequest, odgovor.StatusCode);
    }
}

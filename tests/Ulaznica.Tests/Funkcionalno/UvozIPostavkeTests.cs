using System.Net;
using System.Text;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Funkcionalno;

[Trait("Category", "Funkcionalno")]
public sealed class UvozIPostavkeTests(UlaznicaFactory factory) : IClassFixture<UlaznicaFactory>
{
    private const string NoviDogadjaji = """
        <?xml version="1.0" encoding="utf-8"?>
        <dogadjaji>
          <dogadjaj>
            <naziv>Božićni koncert</naziv>
            <mjesto>Katedrala, Zagreb</mjesto>
            <datum>2026-12-12T20:00</datum>
            <cijena>25.00</cijena>
            <kapacitet>300</kapacitet>
          </dogadjaj>
          <dogadjaj>
            <naziv>Kino pod zvijezdama</naziv>
            <mjesto>Zrinjevac, Zagreb</mjesto>
            <datum>2026-12-14T19:00</datum>
            <cijena>5.50</cijena>
            <kapacitet>150</kapacitet>
          </dogadjaj>
        </dogadjaji>
        """;

    private static StringContent Xml(string xml) => new(xml, Encoding.UTF8, "application/xml");

    [Fact]
    public async Task OrganizatorUvoziDogadjajeIzXmla()
    {
        var klijent = await factory.KlijentZaAsync("organizator", "Ovlasteni12345!");

        var odgovor = await klijent.PostAsync("/api/dogadjaji/uvoz", Xml(NoviDogadjaji));

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        var json = await odgovor.CitajJsonAsync();
        Assert.Equal(2, json.GetProperty("uvezeno").GetInt32());
        var prvi = json.GetProperty("dogadjaji")[0];
        Assert.True(prvi.GetProperty("id").GetInt64() > 8);
        Assert.Equal("Božićni koncert", prvi.GetProperty("naziv").GetString());
    }

    [Fact]
    public async Task NeispravanXmlVraca400()
    {
        var klijent = await factory.KlijentZaAsync("admin", "Admin12345!");

        var odgovor = await klijent.PostAsync("/api/dogadjaji/uvoz", Xml("<dogadjaji><dogadjaj>"));

        Assert.Equal(HttpStatusCode.BadRequest, odgovor.StatusCode);
    }

    [Fact]
    public async Task KupacNeMozeUvoziti()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var odgovor = await klijent.PostAsync("/api/dogadjaji/uvoz", Xml(NoviDogadjaji));

        Assert.Equal(HttpStatusCode.Forbidden, odgovor.StatusCode);
    }

    [Fact]
    public async Task PostavkeObavijestiUYamlu()
    {
        var klijent = await factory.KlijentZaAsync("marko", "Marko12345!");
        const string yaml = """
            podsjetnik: true
            satiPrije: 48
            kanal: sms
            """;

        var odgovor = await klijent.PostAsync("/api/postavke", new StringContent(yaml, Encoding.UTF8, "application/yaml"));

        Assert.Equal(HttpStatusCode.OK, odgovor.StatusCode);
        var postavke = (await odgovor.CitajJsonAsync()).GetProperty("postavke");
        Assert.True(postavke.GetProperty("podsjetnik").GetBoolean());
        Assert.Equal(48, postavke.GetProperty("satiPrije").GetInt32());
        Assert.Equal("sms", postavke.GetProperty("kanal").GetString());

        var spremljeno = (await (await klijent.GetAsync("/api/postavke")).CitajJsonAsync()).GetProperty("postavke");
        Assert.Equal(48, spremljeno.GetProperty("satiPrije").GetInt32());
    }

    [Fact]
    public async Task NeispravnePostavkeVracaju400()
    {
        var klijent = await factory.KlijentZaAsync("ana", "Ana12345!");

        var odgovor = await klijent.PostAsync("/api/postavke", new StringContent("satiPrije: puno", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, odgovor.StatusCode);
    }
}

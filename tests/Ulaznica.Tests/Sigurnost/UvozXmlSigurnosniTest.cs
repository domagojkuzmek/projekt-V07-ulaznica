using System.Net;
using System.Text;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

/// <summary>
/// Sigurnosni test: uvoz XML-a ne smije razriješiti vanjski entitet iz lokalne datoteke.
/// </summary>
[Trait("Category", "Sigurnost")]
public sealed class UvozXmlSigurnosniTest(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task UvozOdbijaDtdKojiReferenciraLokalnuDatoteku()
    {
        // Arrange – lokalna datoteka s vrijednošću koja se ne smije vratiti u odgovoru
        var klijent = await factory.KlijentZaAsync("admin", "Admin12345!");
        var sadrzajDatoteke = $"xxe-test-{Guid.NewGuid():N}";
        var putanjaDatoteke = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(putanjaDatoteke, sadrzajDatoteke);

        try
        {
            var uriDatoteke = new Uri(putanjaDatoteke).AbsoluteUri;
            var xml = $"""
                <!DOCTYPE dogadjaji [<!ENTITY naziv SYSTEM "{uriDatoteke}">]>
                <dogadjaji>
                  <dogadjaj>
                    <naziv>&naziv;</naziv>
                    <mjesto>Zagreb</mjesto>
                    <datum>2026-12-12T20:00</datum>
                    <cijena>25.00</cijena>
                    <kapacitet>300</kapacitet>
                  </dogadjaj>
                </dogadjaji>
                """;

            // Act
            var odgovor = await klijent.PostAsync(
                "/api/dogadjaji/uvoz",
                new StringContent(xml, Encoding.UTF8, "application/xml"));

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, odgovor.StatusCode);
            Assert.DoesNotContain(sadrzajDatoteke, await odgovor.Content.ReadAsStringAsync());
        }
        finally
        {
            File.Delete(putanjaDatoteke);
        }
    }
}

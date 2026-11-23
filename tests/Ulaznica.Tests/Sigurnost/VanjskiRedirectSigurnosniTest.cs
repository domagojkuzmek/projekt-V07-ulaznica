using System.Net;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

/// Sigurnosni test: provjerava da prijava ne preusmjerava korisnika
/// na vanjske URL adrese zadane kroz returnUrl
///
[Trait("Category", "Sigurnost")]
public sealed class VanjskiRedirectSigurnosniTest(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Theory]
    [InlineData("https://primjer.example")]
    [InlineData("//primjer.example")]
    [InlineData("/\\primjer.example")]
    [InlineData("primjer.example")]
    public async Task PrijavaSaNeLokalnimReturnUrlPreusmjeravaNaPocetnuStranicu(string returnUrl)
    {
        // Arrange – klijent bez automatskog praćenja preusmjeravanja
        var klijent = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing
                .WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var podaci = new Dictionary<string, string>
        {
            ["username"] = "ana",
            ["password"] = "Ana12345!",
            ["returnUrl"] = returnUrl
        };

        // Act
        var odgovor = await klijent.PostAsync(
            "/prijava",
            new FormUrlEncodedContent(podaci)
        );

        // Assert – korisnik ne smije biti preusmjeren na vanjsku stranicu
        Assert.Equal(HttpStatusCode.Redirect, odgovor.StatusCode);

        var lokacija = odgovor.Headers.Location;

        Assert.NotNull(lokacija);
        Assert.False(lokacija.IsAbsoluteUri);
        Assert.Equal("/", lokacija!.OriginalString);
    }
}

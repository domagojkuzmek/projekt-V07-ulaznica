using Microsoft.Extensions.DependencyInjection;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

/// <summary>
/// Sigurnosni test: provjerava generira li se različita hash
/// vrijednost za istu lozinku zbog korištenja jedinstvenog salta.
/// </summary>
[Trait("Category", "Sigurnost")]
public sealed class HashiranjeLozinkeTest(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public void IstaLozinkaMoraImatiRazliciteHashVrijednosti()
    {
        // Arrange – priprema iste lozinke
        var lozinka = "TestLozinka123!";
        var servis = factory.Services.GetRequiredService<LozinkeServis>();

        // Act – dva puta hashiramo istu lozinku
        var hash1 = servis.Hashiraj(lozinka);
        var hash2 = servis.Hashiraj(lozinka);

        // Assert – hash vrijednosti moraju biti različite
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void KorisniciUBaziIstiTekstLozinkeDobivajuRazliciteHashVrijednosti()
    {
        var servis = factory.Services.GetRequiredService<KorisniciServis>();
        var lozinke = factory.Services.GetRequiredService<LozinkeServis>();
        var baza = factory.Services.GetRequiredService<Baza>();
        var lozinka = "IstaLozinka123!";
        var prviUsername = $"hash-{Guid.NewGuid():N}";
        var drugiUsername = $"hash-{Guid.NewGuid():N}";

        Assert.NotNull(servis.Registriraj(prviUsername, lozinka, "", "", ""));
        Assert.NotNull(servis.Registriraj(drugiUsername, lozinka, "", "", ""));

        var hash1 = baza.Upit("SELECT password_hash FROM korisnici WHERE username = $u", ("$u", prviUsername)).Single().Tekst("password_hash");
        var hash2 = baza.Upit("SELECT password_hash FROM korisnici WHERE username = $u", ("$u", drugiUsername)).Single().Tekst("password_hash");

        Assert.NotEqual(hash1, hash2);
        Assert.True(lozinke.Provjeri(hash1, lozinka));
        Assert.True(lozinke.Provjeri(hash2, lozinka));
    }

    [Fact]
    public void PocetniKorisniciTakodjerImajuIspravneBcryptSažetke()
    {
        var servis = factory.Services.GetRequiredService<KorisniciServis>();

        Assert.True(servis.PronadjiPoKorisnickomImenu("ana") is { } ana
            && factory.Services.GetRequiredService<LozinkeServis>().Provjeri(ana.PasswordHash, "Ana12345!"));
        Assert.True(servis.PronadjiPoKorisnickomImenu("marko") is { } marko
            && factory.Services.GetRequiredService<LozinkeServis>().Provjeri(marko.PasswordHash, "Marko12345!"));
    }
}

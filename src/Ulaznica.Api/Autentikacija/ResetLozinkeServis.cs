using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ulaznica.Api.Infrastruktura;
using Microsoft.AspNetCore.WebUtilities;

namespace Ulaznica.Api.Autentikacija;

public enum RezultatPromjeneLozinke
{
    Promijenjena,
    NeispravanToken,
    PrekratkaLozinka,
}

/// <summary>Obnova zaboravljene lozinke: token se šalje e-poštom, a zatim se njime postavlja nova lozinka.</summary>
public sealed class ResetLozinkeServis(
    Baza baza, KorisniciServis korisnici, Posta posta, PostavkeAplikacije postavke, ILogger<ResetLozinkeServis> logger)
{
    public const int NajmanjaDuljinaLozinke = 8;
    private static readonly TimeSpan TrajanjeTokena = TimeSpan.FromMinutes(15);

    public void ZatraziPromjenu(string? username)
    {
        var korisnik = string.IsNullOrWhiteSpace(username) ? null : korisnici.PronadjiPoKorisnickomImenu(username.Trim());
        if (korisnik is null)
        {
            logger.LogInformation("Zahtjev za obnovu lozinke za nepostojeći račun");
            return;
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        baza.Izvrsi(
            "INSERT INTO reset_tokeni (token_hash, korisnik_id, vrijedi_do, iskoristen) VALUES ($h, $k, $v, 0)",
            ("$h", Sazetak(token)), ("$k", korisnik.Id), ("$v", DateTimeOffset.UtcNow.Add(TrajanjeTokena).ToUnixTimeSeconds()));

        posta.Posalji(korisnik.Username, korisnik.Email, "Obnova lozinke", $"""
            Poštovani,

            zatražena je obnova lozinke za korisnički račun {korisnik.Username}.
            Novu lozinku možete postaviti na poveznici:
            {postavke.JavniUrl}/reset?token={Uri.EscapeDataString(token)}

            Token: {token}

            Ako niste zatražili obnovu lozinke, zanemarite ovu poruku.
            """);
        logger.LogInformation("Poslane su upute za obnovu lozinke korisniku {Username}", korisnik.Username);
    }

    public RezultatPromjeneLozinke PromijeniLozinku(string? token, string? novaLozinka)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return RezultatPromjeneLozinke.NeispravanToken;
        }
        if (novaLozinka is null || novaLozinka.Length < NajmanjaDuljinaLozinke)
        {
            return RezultatPromjeneLozinke.PrekratkaLozinka;
        }

        using var veza = baza.Otvori();
        using var transakcija = veza.BeginTransaction();
        var zapis = Baza.Upit(veza, transakcija,
            "SELECT id, korisnik_id FROM reset_tokeni WHERE token_hash = $h AND iskoristen = 0 AND vrijedi_do > $sada",
            ("$h", Sazetak(token.Trim())), ("$sada", DateTimeOffset.UtcNow.ToUnixTimeSeconds())).FirstOrDefault();
        if (zapis is null)
        {
            return RezultatPromjeneLozinke.NeispravanToken;
        }
        var korisnikId = zapis.Broj("korisnik_id");
        Baza.Izvrsi(veza, transakcija, "UPDATE reset_tokeni SET iskoristen = 1 WHERE korisnik_id = $k", ("$k", korisnikId));
        korisnici.PostaviLozinku(veza, transakcija, korisnikId, novaLozinka);
        transakcija.Commit();

        logger.LogInformation("Promijenjena je lozinka korisnika s ID-om {Id}", korisnikId);
        return RezultatPromjeneLozinke.Promijenjena;
    }

    private static string Sazetak(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

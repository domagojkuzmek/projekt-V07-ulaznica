using Microsoft.AspNetCore.Mvc;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;

using System.Text.Encodings.Web;

namespace Ulaznica.Api.Prodaja;

/// <summary>HTML ulaznice za ispis.</summary>
[PotrebnaPrijava]
public sealed class IspisController(Baza baza) : ControllerBase
{
    [HttpGet("/ispis/narudzbe/{id:long}")]
    public IActionResult IspisUlaznice(long id)
    {
        var korisnik = HttpContext.Korisnik();
        var n = baza.Upit("""
            SELECT n.id, n.kupac, n.kolicina, n.ime_na_ulaznici, n.kreirano, d.naziv, d.mjesto, d.datum, d.cijena
            FROM narudzbe n JOIN dogadjaji d ON d.id = n.dogadjaj_id
            WHERE n.id = $id
            """, ("$id", id)).FirstOrDefault();
        if (n is null || (n.Tekst("kupac") != korisnik.Username && !korisnik.JeAdmin))
        {
            return NotFound(new { greska = "Narudžba ne postoji." });
        }

        var datum = n.Tekst("datum").Replace('T', ' ');
        var kod = $"UL-{n.Broj("id"):D6}";

        //Ispravak dodavanje encodinga
        var tijelo = $"""
            <h1>{HtmlEncoder.Default.Encode(n.Tekst("naziv"))}</h1>
            <table>
            <tr><th>Ime na ulaznici</th><td>{HtmlEncoder.Default.Encode(n.Tekst("ime_na_ulaznici"))}</td></tr>
            <tr><th>Mjesto</th><td>{HtmlEncoder.Default.Encode(n.Tekst("mjesto"))}</td></tr>
            <tr><th>Datum i vrijeme</th><td>{HtmlEncoder.Default.Encode(datum)}</td></tr>
            <tr><th>Broj ulaznica</th><td>{n.Broj("kolicina")}</td></tr>
            <tr><th>Kod ulaznice</th><td><strong>{HtmlEncoder.Default.Encode(kod)}</strong></td></tr>
            </table>
            <p>Ulaznicu pokažite na ulazu (ispisanu ili na mobitelu). Kupljeno: {HtmlEncoder.Default.Encode(n.Tekst("kreirano"))}</p>
            """;
        return HtmlStranica.Odgovor($"Ulaznica {kod}", tijelo);
    }
}

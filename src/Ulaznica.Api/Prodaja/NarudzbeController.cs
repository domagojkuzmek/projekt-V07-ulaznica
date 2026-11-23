using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;

namespace Ulaznica.Api.Prodaja;

/// <summary>Narudžbe ulaznica prijavljenog kupca.</summary>
[ApiController]
[Route("api/narudzbe")]
[PotrebnaPrijava]
public sealed class NarudzbeController(Baza baza, NaplataKlijent naplata, ILogger<NarudzbeController> logger) : ControllerBase
{
    private const int NajviseUlaznica = 4;

    public sealed record NovaNarudzba(long? DogadjajId, int? Kolicina, string? ImeNaUlaznici, string? Oib);

    [HttpGet("moje")]
    public IActionResult Moje()
    {
        var redci = baza.Upit("""
            SELECT n.id, n.dogadjaj_id, d.naziv, d.mjesto, d.datum, d.cijena, n.kolicina, n.ime_na_ulaznici, n.oib, n.kreirano
            FROM narudzbe n JOIN dogadjaji d ON d.id = n.dogadjaj_id
            WHERE n.kupac = $k
            ORDER BY n.id
            """, ("$k", HttpContext.Korisnik().Username));
        return Ok(redci.Select(r => new
        {
            id = r.Broj("id"),
            dogadjajId = r.Broj("dogadjaj_id"),
            dogadjaj = r.Tekst("naziv"),
            mjesto = r.Tekst("mjesto"),
            datum = r.Tekst("datum"),
            kolicina = r.Broj("kolicina"),
            ukupno = r.Iznos("cijena") * r.Broj("kolicina"),
            imeNaUlaznici = r.Tekst("ime_na_ulaznici"),
            oib = Oib.Maskiraj(r.Tekst("oib")),
            kreirano = r.Tekst("kreirano"),
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Stvori([FromBody] NovaNarudzba zahtjev)
    {
        var korisnik = HttpContext.Korisnik();
        if (zahtjev.DogadjajId is not long dogadjajId || !Oib.JeIspravan(zahtjev.Oib))
        {
            return BadRequest(new { greska = "Neispravni podaci narudžbe (događaj i OIB od 11 znamenki su obavezni)." });
        }
        var kolicina = zahtjev.Kolicina ?? 1;
        var imeNaUlaznici = (zahtjev.ImeNaUlaznici ?? string.Empty).Trim();
        if (kolicina is < 1 or > NajviseUlaznica || imeNaUlaznici.Length is 0 or > 100)
        {
            return BadRequest(new { greska = "Količina mora biti od 1 do 4, a ime na ulaznici obavezno (najviše 100 znakova)." });
        }
        logger.LogInformation(
            "Nova narudžba za korisnika {Username}, događaj {DogadjajId}, količina {Kolicina}, OIB {Oib}",
            korisnik.Username, dogadjajId, kolicina, Oib.Maskiraj(zahtjev.Oib!));

        var kreirano = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        const string unosNarudzbe =
            "INSERT INTO narudzbe (dogadjaj_id, kupac, kolicina, ime_na_ulaznici, oib, kreirano) VALUES ($d, $k, $q, $i, $o, $c)";

        using var veza = baza.Otvori();
        using var transakcija = veza.BeginTransaction();
        var cijenaObjekt = Baza.Skalar(veza, transakcija, """
            UPDATE dogadjaji
            SET prodano = prodano + $q
            WHERE id = $id AND prodano + $q <= kapacitet
            RETURNING cijena
            """, ("$q", kolicina), ("$id", dogadjajId));
        if (cijenaObjekt is null)
        {
            var postoji = Baza.Skalar(veza, transakcija, "SELECT 1 FROM dogadjaji WHERE id = $id", ("$id", dogadjajId));
            return postoji is null
                ? NotFound(new { greska = "Događaj ne postoji." })
                : Conflict(new { greska = "Nema slobodnih mjesta." });
        }
        var ukupno = Convert.ToDecimal(cijenaObjekt, CultureInfo.InvariantCulture) * kolicina;

        await naplata.AutorizirajAsync(korisnik.Username, ukupno, HttpContext.RequestAborted);

        var id = Baza.Umetni(veza, transakcija, unosNarudzbe,
            ("$d", dogadjajId), ("$k", korisnik.Username), ("$q", kolicina), ("$i", imeNaUlaznici), ("$o", zahtjev.Oib), ("$c", kreirano));

        transakcija.Commit();
        return StatusCode(StatusCodes.Status201Created, new { id, dogadjajId, kolicina, imeNaUlaznici, ukupno, kreirano });
    }

    /// <summary>Storno narudžbe.</summary>
    [HttpDelete("{id:long}")]
    public IActionResult Storniraj(long id)
    {
        var korisnik = HttpContext.Korisnik();
        using var veza = baza.Otvori();
        using var transakcija = veza.BeginTransaction();
        var narudzba = Baza.Upit(veza, transakcija,
            "SELECT id, dogadjaj_id, kupac, kolicina FROM narudzbe WHERE id = $id", ("$id", id)).FirstOrDefault();
        if (narudzba is null)
        {
            return NotFound(new { greska = "Narudžba ne postoji." });
        }

        if (!(korisnik.Username == narudzba["kupac"]?.ToString()))
        {
            return NotFound(new { greska = "Narudžba ne postoji." });
        }

        Baza.Izvrsi(veza, transakcija, "DELETE FROM narudzbe WHERE id = $id", ("$id", id));
        Baza.Izvrsi(veza, transakcija, "UPDATE dogadjaji SET prodano = MAX(prodano - $q, 0) WHERE id = $d",
            ("$q", narudzba.Broj("kolicina")), ("$d", narudzba.Broj("dogadjaj_id")));
        transakcija.Commit();

        logger.LogInformation("Korisnik {Username} je stornirao narudžbu {Id}", korisnik.Username, id);
        return NoContent();
    }
}

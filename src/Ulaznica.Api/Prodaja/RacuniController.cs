using Microsoft.AspNetCore.Mvc;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;

namespace Ulaznica.Api.Prodaja;

public enum FormatDokumenta
{
    Json,
    Pdf,
}

/// <summary>Računi kupca: popis, prikaz (JSON/PDF) i preuzimanje datoteke.</summary>
[ApiController]
[Route("api/racuni")]
[PotrebnaPrijava]
public sealed class RacuniController(Baza baza, PostavkeAplikacije postavke, ILogger<RacuniController> logger) : ControllerBase
{
    [HttpGet("moji")]
    public IActionResult Moji()
    {
        var redci = baza.Upit(
            "SELECT id, narudzba_id, datoteka FROM racuni WHERE kupac = $k ORDER BY id", ("$k", HttpContext.Korisnik().Username));
        return Ok(redci.Select(r => new { id = r.Broj("id"), narudzbaId = r.Broj("narudzba_id"), datoteka = r.Tekst("datoteka") }));
    }

    /// <summary><c>GET /api/racuni/{id}?format=json|pdf</c></summary>
    [HttpGet("{id:long}")]
    public IActionResult Dohvati(long id, [FromQuery] string? format)
    {
        var korisnik = HttpContext.Korisnik();
        var racun = baza.Upit("SELECT id, narudzba_id, kupac, datoteka FROM racuni WHERE id = $id", ("$id", id)).FirstOrDefault();
        if (racun is null)
        {
            return NotFound(new { greska = "Račun ne postoji." });
        }

        if (!SmijePristupiti(korisnik, racun))
        {
            return NotFound(new { greska = "Račun ne postoji." });
        }
        if (!Enum.TryParse<FormatDokumenta>(format, ignoreCase: true, out var oblik) || !Enum.IsDefined(oblik))
        {
            return BadRequest(new { greska = "Nepodržan format (dopušteno: json, pdf)." });
        }
        return Izvoz(racun, oblik);
    }

    /// <summary><c>GET /api/racuni/preuzmi?datoteka=racun-ana-2026-0001.txt</c></summary>
    [HttpGet("preuzmi")]
    public IActionResult Preuzmi([FromQuery] string? datoteka)
    {
        var korisnik = HttpContext.Korisnik();
        if (string.IsNullOrWhiteSpace(datoteka))
        {
            return BadRequest(new { greska = "Naziv datoteke je obavezan." });
        }

        var racun = baza.Upit("""
            SELECT datoteka
            FROM racuni
            WHERE datoteka = $datoteka AND ($admin = 1 OR kupac = $kupac)
            """,
            ("$datoteka", datoteka),
            ("$admin", korisnik.JeAdmin ? 1 : 0),
            ("$kupac", korisnik.Username)).FirstOrDefault();
        if (racun is null)
        {
            return NotFound(new { greska = "Datoteka ne postoji." });
        }

        var nazivDatoteke = racun.Tekst("datoteka");
        var korijenskaMapa = Path.GetFullPath(postavke.DirektorijDokumenata);
        if (Path.IsPathRooted(nazivDatoteke)
            || !string.Equals(Path.GetFileName(nazivDatoteke), nazivDatoteke, StringComparison.Ordinal))
        {
            return NotFound(new { greska = "Datoteka ne postoji." });
        }

        var putanja = Path.GetFullPath(nazivDatoteke, korijenskaMapa);
        var relativnaPutanja = Path.GetRelativePath(korijenskaMapa, putanja);
        if (Path.IsPathRooted(relativnaPutanja)
            || relativnaPutanja == ".."
            || relativnaPutanja.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativnaPutanja.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            || !System.IO.File.Exists(putanja))
        {
            return NotFound(new { greska = "Datoteka ne postoji." });
        }
        logger.LogInformation("Korisnik {Username} preuzima datoteku {Datoteka}", korisnik.Username, nazivDatoteke);
        return File(System.IO.File.ReadAllBytes(putanja), "text/plain; charset=utf-8", nazivDatoteke);
    }

    private static bool SmijePristupiti(PrijavljeniKorisnik korisnik, Dictionary<string, object?> racun) =>
        korisnik.JeAdmin || racun.Tekst("kupac") == korisnik.Username;

    private IActionResult Izvoz(Dictionary<string, object?> racun, FormatDokumenta oblik)
    {
        var sadrzaj = ProcitajSadrzaj(racun);
        return oblik switch
        {
            FormatDokumenta.Pdf => File(JednostavniPdf.Izradi($"Racun {racun.Broj("id")}", sadrzaj), "application/pdf", $"racun-{racun.Broj("id")}.pdf"),
            _ => Ok(new
            {
                id = racun.Broj("id"),
                narudzbaId = racun.Broj("narudzba_id"),
                datoteka = racun.Tekst("datoteka"),
                sadrzaj,
            }),
        };
    }

    private string ProcitajSadrzaj(Dictionary<string, object?> racun) =>
        System.IO.File.ReadAllText(Path.Combine(postavke.DirektorijDokumenata, Path.GetFileName(racun.Tekst("datoteka"))));
}

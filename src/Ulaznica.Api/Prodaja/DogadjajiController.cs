using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;

namespace Ulaznica.Api.Prodaja;

/// <summary>Događaji u prodaji i uvoz novih događaja iz XML-a.</summary>
[ApiController]
[Route("api/dogadjaji")]
public sealed class DogadjajiController(Baza baza, ILogger<DogadjajiController> logger) : ControllerBase
{
    private const int NajvecaVelicinaXml = 100_000;
    private const string FormatDatuma = "yyyy-MM-dd'T'HH:mm";

    private sealed record NoviDogadjaj(string Naziv, string Mjesto, string Datum, decimal Cijena, int Kapacitet);

    private static readonly Dictionary<string, string> StupciZaSortiranje = new(StringComparer.OrdinalIgnoreCase)
    {
        ["naziv"] = "naziv",
        ["mjesto"] = "mjesto",
        ["datum"] = "datum",
        ["cijena"] = "cijena",
    };

    /// <summary><c>GET /api/dogadjaji?sort=datum&amp;smjer=asc</c></summary>
    [HttpGet]
    public IActionResult Popis([FromQuery] string? sort, [FromQuery] string? smjer)
    {
        sort = string.IsNullOrWhiteSpace(sort) ? "datum" : sort;
        smjer = string.IsNullOrWhiteSpace(smjer) ? "asc" : smjer;

        if (!StupciZaSortiranje.TryGetValue(sort, out var stupac))
        {
            return BadRequest(new { greska = "Neispravan parametar sortiranja." });
        }
        var redoslijed = smjer.ToLowerInvariant() switch
        {
            "asc" => "ASC",
            "desc" => "DESC",
            _ => null,
        };
        if (redoslijed is null)
        {
            return BadRequest(new { greska = "Neispravan parametar sortiranja." });
        }
        var sql = (stupac, redoslijed) switch
        {
            ("naziv", "ASC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY naziv ASC, id",
            ("naziv", "DESC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY naziv DESC, id",
            ("mjesto", "ASC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY mjesto ASC, id",
            ("mjesto", "DESC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY mjesto DESC, id",
            ("datum", "ASC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY datum ASC, id",
            ("datum", "DESC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY datum DESC, id",
            ("cijena", "ASC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY cijena ASC, id",
            ("cijena", "DESC") => "SELECT id, naziv, mjesto, datum, cijena, kapacitet, prodano FROM dogadjaji ORDER BY cijena DESC, id",
            _ => null,
        };
        if (sql is null)
        {
            return BadRequest(new { greska = "Neispravan parametar sortiranja." });
        }

        var dogadjaji = baza.Upit(sql).Select(r => new
        {
            id = r.Broj("id"),
            naziv = r.Tekst("naziv"),
            mjesto = r.Tekst("mjesto"),
            datum = r.Tekst("datum"),
            cijena = r.Iznos("cijena"),
            kapacitet = r.Broj("kapacitet"),
            prodano = r.Broj("prodano"),
            slobodno = Math.Max(0, r.Broj("kapacitet") - r.Broj("prodano")),
        });
        return Ok(dogadjaji);
    }

    /// <summary>
    /// Uvoz događaja: <c>&lt;dogadjaji&gt;&lt;dogadjaj&gt;&lt;naziv&gt;…&lt;/naziv&gt;&lt;mjesto&gt;…&lt;/mjesto&gt;
    /// &lt;datum&gt;2026-12-12T20:00&lt;/datum&gt;&lt;cijena&gt;25.00&lt;/cijena&gt;&lt;kapacitet&gt;300&lt;/kapacitet&gt;&lt;/dogadjaj&gt;&lt;/dogadjaji&gt;</c>
    /// </summary>
    [HttpPost("uvoz")]
    [RequestSizeLimit(NajvecaVelicinaXml)]
    [PotrebnaPrijava(Aplikacija.PovlastenaUloga, Uloge.Admin)]
    public async Task<IActionResult> Uvoz()
    {
        string xml;
        using (var citac = new StreamReader(Request.Body, Encoding.UTF8))
        {
            xml = await citac.ReadToEndAsync(HttpContext.RequestAborted);
        }
        if (xml.Length > NajvecaVelicinaXml)
        {
            return BadRequest(new { greska = "Datoteka je prevelika." });
        }

        List<NoviDogadjaj> dogadjaji;
        try
        {
            dogadjaji = ProcitajXml(xml);
        }
        catch (Exception ex) when (ex is XmlException or FormatException or OverflowException)
        {
            logger.LogInformation("Uvoz događaja nije uspio: {Poruka}", ex.Message);
            return BadRequest(new { greska = "Neispravan XML." });
        }

        var uvezeni = new List<object>();
        using (var veza = baza.Otvori())
        using (var transakcija = veza.BeginTransaction())
        {
            foreach (var dogadjaj in dogadjaji)
            {
                var id = Baza.Umetni(veza, transakcija,
                    "INSERT INTO dogadjaji (naziv, mjesto, datum, cijena, kapacitet, prodano) VALUES ($n, $m, $d, $c, $k, 0)",
                    ("$n", dogadjaj.Naziv), ("$m", dogadjaj.Mjesto), ("$d", dogadjaj.Datum), ("$c", dogadjaj.Cijena), ("$k", dogadjaj.Kapacitet));
                uvezeni.Add(new { id, naziv = dogadjaj.Naziv });
            }
            transakcija.Commit();
        }

        logger.LogInformation("Korisnik {Username} je uvezao {Broj} događaja", HttpContext.Korisnik().Username, uvezeni.Count);
        return Ok(new { uvezeno = uvezeni.Count, dogadjaji = uvezeni });
    }

    private static List<NoviDogadjaj> ProcitajXml(string xml)
    {
        var postavke = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };
        using var citac = XmlReader.Create(new StringReader(xml), postavke);
        var dokument = XDocument.Load(citac);
        if (dokument.Root is not { Name.LocalName: "dogadjaji" } korijen)
        {
            throw new FormatException("Očekivan je korijenski element <dogadjaji>.");
        }

        var dogadjaji = new List<NoviDogadjaj>();
        foreach (var element in korijen.Elements("dogadjaj"))
        {
            var naziv = Obavezno(element, "naziv");
            var mjesto = Obavezno(element, "mjesto");
            var datum = DateTime.ParseExact(Obavezno(element, "datum"), FormatDatuma, CultureInfo.InvariantCulture);
            var cijena = decimal.Parse(Obavezno(element, "cijena"), NumberStyles.Number, CultureInfo.InvariantCulture);
            var kapacitet = int.Parse(Obavezno(element, "kapacitet"), NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (naziv.Length > 200 || mjesto.Length > 200 || cijena < 0 || kapacitet is < 1 or > 100_000)
            {
                throw new FormatException("Neispravni podaci događaja.");
            }
            dogadjaji.Add(new NoviDogadjaj(naziv, mjesto, datum.ToString(FormatDatuma, CultureInfo.InvariantCulture), cijena, kapacitet));
        }
        if (dogadjaji.Count == 0)
        {
            throw new FormatException("XML ne sadrži nijedan događaj.");
        }
        return dogadjaji;
    }

    private static string Obavezno(XElement element, string naziv)
    {
        var vrijednost = ((string?)element.Element(naziv))?.Trim();
        return string.IsNullOrEmpty(vrijednost) ? throw new FormatException($"Nedostaje element <{naziv}>.") : vrijednost;
    }
}

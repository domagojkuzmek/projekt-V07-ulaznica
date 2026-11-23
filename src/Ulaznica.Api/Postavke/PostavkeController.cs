using System.Text;
using Ulaznica.Api.Autentikacija;
using Ulaznica.Api.Infrastruktura;
using Microsoft.AspNetCore.Mvc;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ulaznica.Api.Postavke;

/// <summary>
/// Korisničke postavke obavijesti u YAML formatu (<c>Content-Type: application/yaml</c> ili <c>text/plain</c>), npr.
/// <code>
/// podsjetnik: true
/// satiPrije: 24
/// kanal: email
/// </code>
/// </summary>
[ApiController]
[Route("api/postavke")]
[PotrebnaPrijava]
public sealed class PostavkeController(Baza baza, ILogger<PostavkeController> logger) : ControllerBase
{
    private const int NajvecaVelicina = 4096;
    private static readonly string[] Kanali = ["email", "sms", "push"];
    private static readonly PostavkeObavijesti Zadano = new(true, 24, "email");

    public sealed record PostavkeObavijesti(bool Podsjetnik, int SatiPrije, string Kanal);

    public sealed class PostavkeYaml
    {
        public bool Podsjetnik { get; set; } = Zadano.Podsjetnik;
        public int SatiPrije { get; set; } = Zadano.SatiPrije;
        public string? Kanal { get; set; } = Zadano.Kanal;
    }

    [HttpGet]
    public IActionResult Dohvati()
    {
        var redak = baza.Upit(
            "SELECT podsjetnik, sati_prije, kanal FROM postavke_obavijesti WHERE korisnik_id = $id",
            ("$id", HttpContext.Korisnik().Id)).FirstOrDefault();
        var postavke = redak is null
            ? Zadano
            : new PostavkeObavijesti(redak.Broj("podsjetnik") == 1, (int)redak.Broj("sati_prije"), redak.Tekst("kanal"));
        return Ok(new { postavke });
    }

    [HttpPost]
    public async Task<IActionResult> Spremi()
    {
        var korisnik = HttpContext.Korisnik();
        string yaml;
        using (var citac = new StreamReader(Request.Body, Encoding.UTF8))
        {
            yaml = await citac.ReadToEndAsync(HttpContext.RequestAborted);
        }
        if (yaml.Length > NajvecaVelicina)
        {
            return BadRequest(new { greska = "Postavke su prevelike." });
        }

        var postavke = Procitaj(yaml);
        if (postavke is null)
        {
            return BadRequest(new { greska = "Neispravne postavke." });
        }

        baza.Izvrsi(
            "INSERT OR REPLACE INTO postavke_obavijesti (korisnik_id, podsjetnik, sati_prije, kanal) VALUES ($id, $p, $s, $k)",
            ("$id", korisnik.Id), ("$p", postavke.Podsjetnik ? 1 : 0), ("$s", postavke.SatiPrije), ("$k", postavke.Kanal));
        logger.LogInformation("Korisnik {Username} je promijenio postavke obavijesti", korisnik.Username);
        return Ok(new { postavke });
    }

    private PostavkeObavijesti? Procitaj(string yaml)
    {
        PostavkeYaml? podaci;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithDuplicateKeyChecking()
                .WithMaximumRecursion(8)
                .Build();
            podaci = deserializer.Deserialize<PostavkeYaml>(new StringReader(yaml));
        }
        catch (YamlException)
        {
            logger.LogInformation("Postavke nisu u ispravnom YAML formatu.");
            return null;
        }
        if (podaci is null)
        {
            return null;
        }

        var podsjetnik = podaci.Podsjetnik;
        var satiPrije = podaci.SatiPrije;
        var kanal = (podaci.Kanal ?? string.Empty).Trim().ToLowerInvariant();
        if (satiPrije is < 1 or > 168 || !Kanali.Contains(kanal))
        {
            return null;
        }
        return new PostavkeObavijesti(podsjetnik, satiPrije, kanal);
    }
}

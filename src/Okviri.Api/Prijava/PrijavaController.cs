using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Okviri.Api.Sigurnost;

namespace Okviri.Api.Prijava;

public sealed record PrijavaZahtjev([Required, MaxLength(100)] string Username, [Required, MaxLength(200)] string Password);

/// <summary>Lokalna provjera lozinke iza ugrađenog rate limitera (politika "prijava").</summary>
[ApiController]
public sealed class PrijavaController(LokalniKorisnici korisnici) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(Politike.Prijava)]
    [HttpPost("/api/auth/login")]
    public IActionResult Prijava(PrijavaZahtjev zahtjev)
    {
        if (!korisnici.Provjeri(zahtjev.Username, zahtjev.Password))
        {
            return Unauthorized(new { greska = "Neispravno korisničko ime ili lozinka." });
        }
        // U ovom primjeru tokene izdaje Keycloak; lokalna prijava samo potvrđuje vjerodajnice.
        return Ok(new { username = zahtjev.Username, poruka = "Vjerodajnice su ispravne." });
    }
}

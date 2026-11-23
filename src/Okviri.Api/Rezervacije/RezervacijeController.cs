using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Okviri.Api.Obavijesti;
using Okviri.Api.Sigurnost;

namespace Okviri.Api.Rezervacije;

[ApiController]
public sealed class RezervacijeController(
    RezervacijeRepozitorij rezervacije, IAuthorizationService autorizacija, ObavijestiKlijent obavijesti)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/api/health")]
    public IActionResult Health() => Ok(new { status = "UP" });

    /// <summary>Korisnik dolazi iz provjerenog tokena (User), nikad iz parametra zahtjeva.</summary>
    [HttpGet("/api/rezervacije/moje")]
    public IReadOnlyList<Rezervacija> Moje() => rezervacije.ZaPacijenta(User.Identity!.Name!);

    /// <summary>Vlasnik ili ADMIN (resursna politika); inače 404 – ne otkrivamo postoji li tuđi zapis.</summary>
    [HttpDelete("/api/rezervacije/{id:long}")]
    public async Task<IActionResult> Obrisi(long id)
    {
        var rezervacija = rezervacije.Pronadi(id);
        if (rezervacija is null
            || !(await autorizacija.AuthorizeAsync(User, rezervacija, Politike.VlasnikIliAdmin)).Succeeded)
        {
            return NotFound(new { greska = "Zapis nije pronađen." });
        }
        rezervacije.Obrisi(id);
        obavijesti.Otkazano(rezervacija);
        return NoContent();
    }

    [Authorize(Policy = Politike.Admin)]
    [HttpGet("/api/admin/rezervacije")]
    public IReadOnlyList<Rezervacija> Sve() => rezervacije.Sve();
}

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Okviri.Api.Rezervacije;

namespace Okviri.Api.Obavijesti;

/// <summary>
/// Postavke vanjskog servisa za obavijesti. ApiKljuc je TAJNA: u razvoju
/// <c>dotnet user-secrets set "Obavijesti:ApiKljuc" "..."</c>, u produkciji varijabla okruženja
/// <c>Obavijesti__ApiKljuc</c> ili trezor (Key Vault). Nikad u appsettings.json ni u gitu.
/// </summary>
public sealed class ObavijestiPostavke
{
    [Required, MinLength(16)]
    public string ApiKljuc { get; set; } = "";

    [Required]
    public Uri Adresa { get; set; } = new("https://obavijesti.example/api/");
}

/// <summary>Simulacija klijenta (u stvarnosti HttpClient sa zaglavljem X-Api-Key).</summary>
public sealed class ObavijestiKlijent(IOptions<ObavijestiPostavke> postavke, ILogger<ObavijestiKlijent> log)
{
    public int Poslano { get; private set; }

    public void Otkazano(Rezervacija rezervacija)
    {
        _ = postavke.Value.ApiKljuc; // ključ se koristi, ali se NIKAD ne logira
        Poslano++;
        log.LogInformation("Obavijest o otkazivanju rezervacije {Id} poslana na {Adresa}",
            rezervacija.Id, postavke.Value.Adresa.Host);
    }
}

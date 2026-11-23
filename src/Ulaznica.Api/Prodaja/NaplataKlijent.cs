namespace Ulaznica.Api.Prodaja;

/// <summary>
/// Klijent platnog sustava: autorizacija plaćanja karticom prije izdavanja ulaznica. U lokalnom okruženju
/// poziv se simulira uz uobičajeno mrežno kašnjenje i uvijek uspijeva.
/// </summary>
public sealed class NaplataKlijent(ILogger<NaplataKlijent> logger)
{
    private static readonly TimeSpan Kasnjenje = TimeSpan.FromMilliseconds(150);

    public async Task AutorizirajAsync(string kupac, decimal iznos, CancellationToken cancellationToken)
    {
        await Task.Delay(Kasnjenje, cancellationToken);
        logger.LogInformation("Plaćanje autorizirano: kupac {Kupac}, iznos {Iznos} EUR", kupac, iznos);
    }
}

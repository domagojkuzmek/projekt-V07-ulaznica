using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Ulaznica.Tests.Infrastruktura;

/// <summary>
/// Pokreće cijelu aplikaciju u memoriji (TestServer). Svaka instanca ima svoju bazu (svjež početni sadržaj)
/// i svoje privremene mape za dokumente i poštu, pa testovi različitih klasa ne dijele stanje.
/// </summary>
public class UlaznicaFactory : WebApplicationFactory<Program>
{
    public UlaznicaFactory()
    {
        var mapa = Path.Combine(Path.GetTempPath(), "ulaznica-testovi", Guid.NewGuid().ToString("N"));
        DirektorijDokumenata = Path.Combine(mapa, "dokumenti");
        DirektorijPoste = Path.Combine(mapa, "posta");
    }

    public string DirektorijDokumenata { get; }

    public string DirektorijPoste { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, konfiguracija) => konfiguracija.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["App:StorageDir"] = DirektorijDokumenata,
                ["App:MailboxDir"] = DirektorijPoste,
            }));
    }
}

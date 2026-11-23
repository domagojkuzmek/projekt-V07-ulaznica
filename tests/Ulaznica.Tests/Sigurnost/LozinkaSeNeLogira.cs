using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Ulaznica.Tests.Infrastruktura;
using Xunit;

namespace Ulaznica.Tests.Sigurnost;

[Trait("Category", "Sigurnost")]
public sealed class LozinkaSeNeLogira(UlaznicaFactory factory)
    : IClassFixture<UlaznicaFactory>
{
    [Fact]
    public async Task LozinkeSeNeZapisujuOibJeMaskiranINeuspjelaPrijavaSeUpozorava()
    {
        using var provider = new CapturingLoggerProvider();
        using var testFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        using var klijent = testFactory.CreateClient();
        const string pogresnaLozinka = "PogresnaLozinka123!";
        const string oib = "48291736504";

        var neuspjelaPrijava = await klijent.PostAsJsonAsync(
            "/api/auth/login", new { username = "ana", password = pogresnaLozinka });
        Assert.Equal(HttpStatusCode.Unauthorized, neuspjelaPrijava.StatusCode);

        var token = await klijent.PrijaviAsync("marko", "Marko12345!");
        klijent.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var narudzba = await klijent.PostAsJsonAsync("/api/narudzbe", new
        {
            dogadjajId = 7,
            kolicina = 1,
            imeNaUlaznici = "Marko",
            oib,
        });
        Assert.Equal(HttpStatusCode.Created, narudzba.StatusCode);

        var zapisi = provider.Zapisi.ToArray();
        Assert.Contains(zapisi, zapis =>
            zapis.Razina == LogLevel.Warning
            && zapis.Poruka.Contains("Neuspjela prijava", StringComparison.Ordinal));
        Assert.Contains(zapisi, zapis =>
            zapis.Poruka.Contains("OIB *******6504", StringComparison.Ordinal));
        Assert.DoesNotContain(zapisi, zapis =>
            zapis.Poruka.Contains(pogresnaLozinka, StringComparison.Ordinal)
            || zapis.Poruka.Contains("Marko12345!", StringComparison.Ordinal)
            || zapis.Poruka.Contains(oib, StringComparison.Ordinal));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Razina, string Poruka)> Zapisi { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                provider.Zapisi.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
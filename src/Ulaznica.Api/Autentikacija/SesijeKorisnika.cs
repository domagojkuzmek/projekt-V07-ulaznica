using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Ulaznica.Api.Autentikacija;

/// <summary>Pristupni tokeni prijavljenih korisnika (32 nasumična bajta, Base64Url), čuvaju se u memoriji.</summary>
public sealed class SesijeKorisnika
{
    public static readonly TimeSpan Trajanje = TimeSpan.FromHours(8);

    private sealed record ZapisSesije(PrijavljeniKorisnik Korisnik, DateTimeOffset Istjece);
    private readonly ConcurrentDictionary<string, ZapisSesije> _sesije = new(StringComparer.Ordinal);

    public string Otvori(PrijavljeniKorisnik korisnik)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        _sesije[token] = new ZapisSesije(korisnik, DateTimeOffset.UtcNow.Add(Trajanje));
        return token;
    }

    public PrijavljeniKorisnik? Pronadji(string token)
    {
        if (!_sesije.TryGetValue(token, out var zapis))
        {
            return null;
        }
        if (zapis.Istjece <= DateTimeOffset.UtcNow)
        {
            _sesije.TryRemove(token, out _);
            return null;
        }
        return zapis.Korisnik;
    }
}

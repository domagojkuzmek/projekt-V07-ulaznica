using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Okviri.Api.Prijava;

public sealed record LokalniKorisnik(string Username);

/// <summary>
/// PasswordHasher&lt;T&gt; (PBKDF2-HMAC-SHA512, nasumična sol, 100 000 iteracija, format s verzijom)
/// koji još prepoznaje stare SHA-256 hasheve bez soli iz T2 i traži njihovu zamjenu (SuccessRehashNeeded).
/// </summary>
public sealed class HasherSMigracijom : PasswordHasher<LokalniKorisnik>
{
    public override PasswordVerificationResult VerifyHashedPassword(
        LokalniKorisnik user, string hashedPassword, string providedPassword)
    {
        if (hashedPassword.Length == 64 && hashedPassword.All(Uri.IsHexDigit))
        {
            var izracunato = SHA256.HashData(Encoding.UTF8.GetBytes(providedPassword));
            return CryptographicOperations.FixedTimeEquals(izracunato, Convert.FromHexString(hashedPassword))
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }
        return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}

/// <summary>Lokalni korisnici – za aplikacije koje (još) imaju vlastitu prijavu kao u T2.</summary>
public sealed class LokalniKorisnici
{
    private readonly IPasswordHasher<LokalniKorisnik> _hasher;
    private readonly ILogger<LokalniKorisnici> _log;
    private readonly ConcurrentDictionary<string, string> _hashevi = new();
    private readonly string _laziHash;

    public LokalniKorisnici(IPasswordHasher<LokalniKorisnik> hasher, ILogger<LokalniKorisnici> log)
    {
        _hasher = hasher;
        _log = log;
        _laziHash = hasher.HashPassword(new LokalniKorisnik("-"), "lazna-lozinka-za-jednako-trajanje");
        _hashevi["ana"] = hasher.HashPassword(new LokalniKorisnik("ana"), "Ana12345!");
        // Hash iz T2 (SHA-256 bez soli) – zamjenjuje se pri prvoj uspješnoj prijavi.
        _hashevi["marko"] = "78bc5813d03977dbf07326fae76c9a3e1504f4c20291cf523b6a95d148a904b9";
    }

    public bool Provjeri(string username, string lozinka)
    {
        var korisnik = new LokalniKorisnik(username);
        if (!_hashevi.TryGetValue(username, out var hash))
        {
            _hasher.VerifyHashedPassword(korisnik, _laziHash, lozinka); // isto trajanje kao za postojećeg
            _log.LogWarning("Neuspjela prijava za korisnika {Korisnik}", username);
            return false;
        }
        var rezultat = _hasher.VerifyHashedPassword(korisnik, hash, lozinka);
        if (rezultat == PasswordVerificationResult.Failed)
        {
            _log.LogWarning("Neuspjela prijava za korisnika {Korisnik}", username);
            return false;
        }
        if (rezultat == PasswordVerificationResult.SuccessRehashNeeded)
        {
            _hashevi[username] = _hasher.HashPassword(korisnik, lozinka);
            _log.LogInformation("Hash lozinke korisnika {Korisnik} nadograđen", username);
        }
        _log.LogInformation("Uspješna prijava korisnika {Korisnik}", username);
        return true;
    }

    public string? Hash(string username) => _hashevi.GetValueOrDefault(username);
}

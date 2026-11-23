using System.Security.Cryptography;
using System.Text;
using BCrypt;

namespace Ulaznica.Api.Autentikacija;

/// <summary>Izračun i provjera sažetka lozinke (stupac <c>korisnici.password_hash</c>).</summary>
public sealed class LozinkeServis
{

    // Ispravak ranjivosti
    public string Hashiraj(string lozinka) =>
        BCrypt.Net.BCrypt.HashPassword(lozinka);
    //Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lozinka))).ToLowerInvariant();

    public bool Provjeri(string spremljeniHash, string lozinka) =>
        BCrypt.Net.BCrypt.Verify(lozinka, spremljeniHash);
}

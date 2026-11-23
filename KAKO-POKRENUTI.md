# Ulaznica (.NET 10) – kako pokrenuti

## Preduvjeti

- .NET SDK 10.0 (`dotnet --version`)
- Visual Studio 2026, JetBrains Rider ili VS Code s ekstenzijom C# Dev Kit

## Pokretanje

```bash
dotnet run --project src/Ulaznica.Api
```

Aplikacija radi na <http://localhost:5080> (profil `http` u `src/Ulaznica.Api/Properties/launchSettings.json`).

- Početna stranica: <http://localhost:5080/>
- Web prijava: <http://localhost:5080/prijava>
- Provjera rada: <http://localhost:5080/api/health>

U Visual Studiju otvori `Ulaznica.sln`, za početni projekt odaberi `Ulaznica.Api` i profil `http`.
U VS Codeu koristi *Run and Debug* (konfiguracija iz `.vscode/launch.json`).

Baza je SQLite u memoriji – pri svakom pokretanju stvara se iznova s početnim podacima. Dokumenti (računi) i
„poslane” e-poruke spremaju se u `src/Ulaznica.Api/data/dokumenti/` i `src/Ulaznica.Api/data/posta/`
(ključevi `App:StorageDir` i `App:MailboxDir` u `appsettings.json`).

## Korisnici

| Korisničko ime | Lozinka | Uloga |
|---|---|---|
| `ana` | `Ana12345!` | KORISNIK (kupac) |
| `marko` | `Marko12345!` | KORISNIK (kupac) |
| `organizator` | `Ovlasteni12345!` | ORGANIZATOR |
| `admin` | `Admin12345!` | ADMIN |

API prijava: `POST /api/auth/login` s tijelom `{"username":"ana","password":"Ana12345!"}`. Token iz odgovora
šalje se u zaglavlju `Authorization: Bearer <token>`. Primjeri svih zahtjeva su u `http/ulaznica.http`.

## Glavni endpointi

| Metoda i putanja | Opis |
|---|---|
| `GET /api/dogadjaji?sort=datum&smjer=asc` | događaji u prodaji (`sort`: naziv, mjesto, datum, cijena) |
| `POST /api/narudzbe` | kupnja `{ "dogadjajId", "kolicina", "imeNaUlaznici", "oib" }` (količina 1–4) |
| `GET /api/narudzbe/moje` | narudžbe prijavljenog kupca |
| `DELETE /api/narudzbe/{id}` | storno narudžbe |
| `GET /ispis/narudzbe/{id}` | HTML ulaznica za ispis |
| `GET /api/racuni/moji` | računi prijavljenog kupca |
| `GET /api/racuni/{id}?format=json` ili `format=pdf` | prikaz računa |
| `GET /api/racuni/preuzmi?datoteka=…` | preuzimanje datoteke računa |
| `POST /api/dogadjaji/uvoz` | uvoz događaja iz XML-a (ORGANIZATOR, ADMIN) |
| `GET`, `POST /api/postavke` | postavke obavijesti (YAML) |
| `POST /api/auth/register`, `/api/auth/reset-zahtjev`, `/api/auth/reset` | registracija i obnova lozinke |

## Testovi

```bash
dotnet test                                           # svi testovi
dotnet test --filter "Category=Funkcionalno"          # funkcionalni testovi
dotnet test --filter "Category=Primjer"               # primjer testa
dotnet test --collect:"XPlat Code Coverage"           # pokrivenost koda (TestResults/**/coverage.cobertura.xml)
```

Testovi pokreću aplikaciju u memoriji (`WebApplicationFactory`), svaka testna klasa sa svojom bazom i privremenim
mapama – aplikacija ne mora biti pokrenuta.

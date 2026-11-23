# ASP.NET Core mehanizmi umjesto ručnih provjera (.NET 10)

Pokretanje: `docker compose up -d` u `../keycloak`, zatim (jednom) tajna pa aplikacija:

```bash
cd src/Okviri.Api
dotnet user-secrets set "Obavijesti:ApiKljuc" "lokalni-kljuc-0123456789"   # tajna izvan repozitorija
dotnet run                                                                 # http://localhost:5080
```

Testovi: `dotnet test` – **ne trebaju Keycloak** (testna shema autentikacije `TestnaShema`).

## Prije (ručna provjera u T2) → poslije (mehanizam okvira)

| Tema (T2) | Prije: ručno | Poslije: ASP.NET Core | Gdje |
|---|---|---|---|
| Prijava i tokeni | `SesijaMiddleware`, `[PotrebnaPrijava]`, tokeni u memoriji | **JwtBearer**: JWT od Keycloaka; potpis (JWKS), `exp`, `iss`, `aud` provjerava okvir | `Program.cs` (1.) |
| Tko je prijavljen | `PrijavljeniKorisnik` iz `HttpContext.Items` | `User.Identity.Name` = `preferred_username`; uloge iz `realm_access.roles` (`IClaimsTransformation`) | `KeycloakUloge` |
| Zaštićene putanje | atribut na svakom kontroleru | **fallback politika** `RequireAuthenticatedUser()` – sve je zaštićeno osim `[AllowAnonymous]` | `Program.cs` (2.) |
| Vlasništvo (IDOR, S01) | `if (rez.Pacijent != korisnik)` u akciji | resursna autorizacija: `IAuthorizationService.AuthorizeAsync(User, rezervacija, "VlasnikIliAdmin")` + `AuthorizationHandler<TZahtjev, TResurs>`; odbijeno → 404 | `VlasnikIliAdmin.cs`, `RezervacijeController` |
| Uloge | `if (korisnik.Uloga == "ADMIN")` | politika `Admin` (`RequireRole`) i `[Authorize(Policy = ...)]` | `Program.cs`, `RezervacijeController` |
| CORS (S04) | `SetIsOriginAllowed(_ => true)` / ručno | `AddCors` + `WithOrigins(Cors:AllowedOrigins)` iz konfiguracije | `Program.cs` (4.) |
| Sigurnosna zaglavlja (S09) | CSP na jednoj stranici | middleware za **svaki** odgovor: CSP, `X-Frame-Options`, `nosniff`, `Referrer-Policy`, `Cache-Control`; HSTS izvan razvoja | `SigurnosnaZaglavlja` |
| Lozinke (S07) | SHA-256 bez soli / ručni BCrypt | `PasswordHasher<T>` (PBKDF2, sol, verzija formata); stari SHA-256 → `SuccessRehashNeeded` → automatska zamjena | `LokalniKorisnici.cs` |
| Pogađanje lozinki | ništa | `AddRateLimiter` + `[EnableRateLimiting("prijava")]`: N pokušaja/min po IP → `429` + `Retry-After` | `Program.cs` (3.), `PrijavaController` |
| Tajne | u `appsettings.json` ili kodu | **user-secrets** u razvoju, varijable okruženja / trezor u produkciji; `ValidateOnStart` ruši pokretanje ako tajna nedostaje | `ObavijestiKlijent.cs`, `.csproj` (`UserSecretsId`) |
| Greške 401/403 | ručni JSON | `IAuthorizationMiddlewareResultHandler` → `{ "greska": "..." }` | `JsonOdgovorAutorizacije` |

Antiforgery (CSRF) nije potreban jer API prima token u zaglavlju `Authorization`; ako vaš T2 projekt zadrži kolačić
`SESSION` i `/prijava` formu, uključite `AddAntiforgery()` / `[ValidateAntiForgeryToken]`.

## Testovi (xUnit + `WebApplicationFactory`)

`OkviriFactory` zamjenjuje JwtBearer testnom shemom (zaglavlja `X-Test-Korisnik`, `X-Test-Uloge` → claimovi
`preferred_username` i `realm_access` kakve daje Keycloak), pa **naša** transformacija uloga, politike i handler
vlasništva rade kao u produkciji.

- `SigurnostTests` – 401, samo vlastite, 404 za tuđu/nepostojeću, ADMIN smije sve, 403 za ADMIN popis, zaglavlja, CORS.
- `LozinkeIPrijavaTests` – sol, migracija SHA-256 hasha pri prijavi, isti odgovor za krivu lozinku i nepostojećeg korisnika.
- `OgranicenjePrijaveTests` – 429 nakon N pokušaja. `PokretanjeTests` – bez `Obavijesti:ApiKljuc` aplikacija se ne pokreće.

## Kako to prenijeti u svoj T2 projekt

1. `Microsoft.AspNetCore.Authentication.JwtBearer` + `AddAuthentication().AddJwtBearer()` (ili zadržite vlastitu
   prijavu, ali s `PasswordHasher<T>` i `AddRateLimiter`).
2. Fallback politika umjesto `[PotrebnaPrijava]`; `SesijaMiddleware` uklonite kad testovi prolaze.
3. Provjere vlasništva → `AuthorizationHandler<TZahtjev, TResurs>`; u akciji samo `AuthorizeAsync(...)`.
4. CORS i tajne iz konfiguracije (`dotnet user-secrets init` / `set`).
5. Pokrenite T2 testove (`dotnet test`) i DevSecOps cjevovod (`../../devsecops`).

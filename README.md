# Tjedan 3 – sigurnosni mehanizmi okvira umjesto ručnih provjera

U Tjednu 2 ste propuste popravljali **ručno** (if-ovi u kontrolerima, vlastiti filtri, vlastiti tokeni).
Ovi primjeri pokazuju kako isto rade **Spring Security**, **ASP.NET Core** i **FastAPI** – na jednom mjestu,
zadano zatvoreno (deny by default) i testirano.

| Mapa | Sadržaj |
|---|---|
| [`keycloak/`](keycloak/README.md) | Keycloak u Dockeru (realm `akademija`, korisnici ana/marko/admin) – izdaje JWT tokene |
| [`java/`](java/README.md) | Spring Boot 3.5 + Spring Security: Resource Server (JWT), `@PreAuthorize` + provjera vlasništva, CORS, zaglavlja, `DelegatingPasswordEncoder`, Bucket4j |
| [`dotnet/`](dotnet/README.md) | ASP.NET Core (.NET 10): JwtBearer, politike + `IAuthorizationHandler` za vlasništvo, `AddRateLimiter`, CORS, zaglavlja, `PasswordHasher<T>`, user-secrets |
| [`python/`](python/README.md) | FastAPI (Python 3.12+): PyJWT + `PyJWKClient` (JWKS), globalna ovisnost „zadano zabranjeno”, `Security(..., scopes=[...])` + ovisnost za vlasništvo, `CORSMiddleware`, zaglavlja, pwdlib (Argon2id), limits, pydantic-settings (`.env`) |

Domena je ista kao MedTermin iz T2: *moje rezervacije*, *brisanje vlastite rezervacije*, *ADMIN smije obrisati bilo koju*.
Testovi ne trebaju Keycloak (lažni JWT / testna shema autentikacije / RSA ključ i JWKS generirani u testu);
`keycloak/provjera.sh` s pokrenutim Keycloakom provjerava sva tri API-ja s pravim tokenima (tako ih provjerava i CI
akademije).

**Zadatak:** u svom T2 projektu zamijenite ručne provjere mehanizmima okvira (tablice „prije → poslije” u
README-ima primjera), a skriveni testovi iz T2 i vaši testovi moraju i dalje prolaziti.

---

## Demonstracija korak po korak (na nastavi ili za samostalno ponavljanje)

Ista priča u sve tri tehnologije: **Keycloak izdaje tokene, a primjer (Java, .NET ili Python) ih provjerava
mehanizmima okvira**. Skripta `demo/demo_okviri.py` šalje stvarne zahtjeve i nakon svakog koraka ispiše što je
dokazano. Treba samo Python 3.10+ (standardna biblioteka) – radi na Windowsu, macOS-u i Linuxu.

### 1. Priprema (prije nastave, ~10 minuta, treba internet)

```bash
cd okviri/keycloak && docker compose pull && docker compose up -d     # Keycloak na http://localhost:8180
cd ../java   && ./mvnw -q -DskipTests package                          # Windows: mvnw.cmd -q -DskipTests package
cd ../dotnet && dotnet restore && dotnet build -c Debug
cd ../python && python -m venv .venv && .venv/bin/pip install -r requirements-dev.txt   # Windows: .venv\Scripts\pip …
cp .env.primjer .env                                                    # i upišite OKVIRI_OBAVIJESTI_API_KLJUC (≥ 16 znakova)
```

Prvo preuzimanje (slika Keycloaka, Maven i NuGet ovisnosti, pip paketi) traje nekoliko minuta – na nastavi
računajte na sporiju mrežu. Keycloak je spreman kad `http://localhost:8180/realms/akademija/.well-known/openid-configuration`
vrati JSON (~30 s nakon pokretanja).

### 2. Pokretanje primjera (svaki u svom terminalu ili iz IDE-a)

| | Java (Spring Security) | .NET (ASP.NET Core) | Python (FastAPI) |
|---|---|---|---|
| Naredba | `./mvnw spring-boot:run` (Windows: `mvnw.cmd spring-boot:run`) | `cd src/Okviri.Api`, jednom `dotnet user-secrets set "Obavijesti:ApiKljuc" "lokalni-kljuc-0123456789"`, zatim `dotnet run` | `.venv` aktiviran, `.env` popunjen, `python -m okviri` |
| Adresa | `http://localhost:8080` | `http://localhost:5080` (profil `http`) | `http://localhost:8000`, Swagger UI na `/docs` |
| IDE | IntelliJ IDEA: `OkviriApplication` → *Run* | Visual Studio 2026 / Rider: `Okviri.sln`, profil **http** | PyCharm / VS Code: desni klik na `run.py` → *Run* |

Za kratku demonstraciju dovoljan je **jedan** primjer – u tehnologiji koju bira većina polaznika. Dobar trenutak
za pokazati „sigurno pri pogrešci”: pokrenite Python primjer **bez** `OKVIRI_OBAVIJESTI_API_KLJUC` (ili .NET bez
user-secretsa) – aplikacija se namjerno ne pokreće jer tajna nedostaje.

### 3. Scenarij (skripta)

```bash
python demo/demo_okviri.py python        # ili java, dotnet; Enter između koraka
python demo/demo_okviri.py java --bez-pauze
python demo/demo_okviri.py --url http://localhost:8000 --bez-keycloaka   # bez Dockera: koraci 1, 2, 8 i 9
```

| Korak | Što se vidi | Što reći / gdje je u kodu |
|---|---|---|
| 1. Javna ruta i zaglavlja | `/api/health` → 200 i CSP, `X-Frame-Options`, `nosniff`, `Referrer-Policy`, `Cache-Control` | zaglavlja za **svaki** odgovor postavlja okvir (S09 u T2 bio je CSP samo na jednoj stranici); HSTS tek preko HTTPS-a |
| 2. Zadano zabranjeno | `/api/rezervacije/moje` bez tokena → 401 + `WWW-Authenticate: Bearer` | ruta nema provjeru u kodu: `anyRequest().authenticated()` / `FallbackPolicy` / globalna ovisnost `zadano_zabranjeno` |
| 3. Što je u JWT-u | `iss`, `aud`, `preferred_username`, `realm_access.roles`, `exp` | potpis (JWKS), `iss`, `aud`, `exp` provjerava okvir; mapiranje uloga na jednom mjestu |
| 4. Samo svoje | ana vidi samo svoje rezervacije | korisnik iz tokena, nikad iz parametra zahtjeva |
| 5. Krivotvoreni tokeni | izmijenjena uloga uz stari potpis → 401; `alg: none` → 401 | usporedba s vlastitim JWT kodom iz T1 (Lab12) |
| 6. Tuđi objekt (S01) | marko briše aninu rezervaciju → **404** | `@PreAuthorize` + `Vlasnistvo` / `AuthorizationHandler` / ovisnost `Vlasnistvo(...)` – u ruti nema `if`-a |
| 7. Uloge | ana → admin popis 403; admin → 200; admin briše markovu → 204 | 401 = tko si?, 403 = ne smiješ, 404 = ne otkrivamo postojanje |
| 8. CORS (S04) | preflight s `http://localhost:5173` dobiva `Access-Control-Allow-Origin`, `https://zlo.example` ne | popis origina iz konfiguracije, bez `*`, bez kolačića |
| 9. Prijava | ista poruka za krivu lozinku i nepostojećeg korisnika; markov SHA-256 hash iz T2 se pri prijavi zamijeni (pogledajte **log aplikacije**); nakon 5 pokušaja u minuti 429 + `Retry-After` | `DelegatingPasswordEncoder` / `PasswordHasher<T>` / pwdlib; Bucket4j / `AddRateLimiter` / limits |

Skripta završava s „Svi koraci su se ponašali kako treba” (izlazni kod 0) ili popisom odstupanja (izlazni kod 1).
Ponovno pokretanje unutar iste minute: korak 9 odmah dobiva 429, a korak 7 javlja 404 jer je rezervacija 2 već
obrisana – ponovno pokrenite primjer za početne podatke.

### 4. Isti zahtjevi ručno (za pokazivanje u terminalu)

```bash
# bash / Git Bash / WSL – token SAMO ZA RAZVOJ (password grant, klijent medtermin-dev-cli)
TOKEN=$(curl -s -X POST http://localhost:8180/realms/akademija/protocol/openid-connect/token \
  -d grant_type=password -d client_id=medtermin-dev-cli -d username=marko --data-urlencode 'password=Marko12345!' \
  | python -c 'import json,sys; print(json.load(sys.stdin)["access_token"])')
curl -i -X DELETE -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/rezervacije/1     # 404
curl -i -X OPTIONS -H "Origin: https://zlo.example" -H "Access-Control-Request-Method: GET" http://localhost:8000/api/rezervacije/moje
```

```powershell
# Windows PowerShell (curl je ondje alias – koristite Invoke-RestMethod / curl.exe)
$t = (Invoke-RestMethod -Method Post http://localhost:8180/realms/akademija/protocol/openid-connect/token `
      -Body @{grant_type='password'; client_id='medtermin-dev-cli'; username='ana'; password='Ana12345!'}).access_token
Invoke-RestMethod http://localhost:5080/api/rezervacije/moje -Headers @{Authorization="Bearer $t"}
```

Za prijavu kakva se koristi u pravom web sučelju (Authorization Code + PKCE) vidi [`keycloak/README.md`](keycloak/README.md).

### 5. Testovi kao dokaz

`./mvnw test` · `dotnet test` · `pytest -v` – testovi ne trebaju Keycloak (lažni ili lokalno potpisani JWT). Dobro je
pokazati jedan test uz odgovarajući korak skripte: npr. „marko briše aninu → 404” je `SigurnostApiTest` (Java),
`SigurnostTests` (.NET) i `test_sigurnost_api.py` (Python). Polaznicima je to obrazac za vlastiti projekt: svaka
zamjena ručne provjere mehanizmom okvira ostaje pokrivena testom.

### 6. Česti problemi

| Simptom | Rješenje |
|---|---|
| Keycloak ne izdaje token / skripta javlja grešku u koraku 3 | `docker compose ps` u `keycloak/`; pričekajte ~30 s nakon pokretanja; port 8180 mora biti slobodan. |
| Ispravan token, a API vraća 401 | Keycloak je pokrenut **nakon** primjera ili je ponovno pokrenut (novi ključevi): ponovno pokrenite primjer. .NET: pokrenite s profilom `http` (Development – dopušta HTTP Keycloak). |
| Python se ne pokreće: nedostaje `OKVIRI_OBAVIJESTI_API_KLJUC` | Namjerno (tajna bez zadane vrijednosti). Upišite je u `.env` (≥ 16 znakova). |
| .NET se ne pokreće: nedostaje `Obavijesti:ApiKljuc` | Namjerno (`ValidateOnStart`). `dotnet user-secrets set "Obavijesti:ApiKljuc" "…"` u `src/Okviri.Api`. |
| `429` već u prvom pokušaju prijave | Brojač iz prethodnog pokretanja: pričekajte minutu ili ponovno pokrenite primjer. |
| `Address already in use` | Port 8080/5080/8000 zauzima druga aplikacija (npr. VulnShop iz T1) – zaustavite je. |

# Keycloak za razvoj (realm `akademija`)

> ⚠️ Samo za lokalni razvoj: `start-dev` (HTTP, bez TLS-a, podaci u kontejneru), lozinke u `realm-akademija.json`,
> port vezan na `127.0.0.1`. U produkciji: `start` s TLS-om, vanjska baza, tajne izvan repozitorija.

## Pokretanje

```bash
cd tjedan-3/okviri/keycloak
docker compose up -d          # prvo pokretanje traje ~30 s
docker compose logs -f        # (Ctrl+C) – čekajte "Listening on: http://0.0.0.0:8080"
```

- Realm: `http://localhost:8180/realms/akademija` (OIDC: `.../.well-known/openid-configuration`)
- Administracija (master realm): `http://localhost:8180/admin` – `kcadmin` / `kcadmin-samo-za-razvoj`
- Korisnička konzola: `http://localhost:8180/realms/akademija/account`

| Korisnik | Lozinka | Realm uloga |
|---|---|---|
| ana | `Ana12345!` | KORISNIK |
| marko | `Marko12345!` | KORISNIK |
| dr.horvat | `Ovlasteni12345!` | LIJECNIK |
| admin | `Admin12345!` | ADMIN |

## Klijenti

| Client ID | Vrsta | Tok | Namjena |
|---|---|---|---|
| `medtermin-spa` | javni | **Authorization Code + PKCE (S256)**, password grant isključen | web sučelje na `http://localhost:5173` |
| `medtermin-api` | povjerljivi, bez tokova | – | samo *audience*: API prihvaća token samo ako `aud` sadrži `medtermin-api` |
| `medtermin-dev-cli` | javni | **password grant – SAMO ZA RAZVOJ** | `curl` i automatske provjere; zastario tok (OAuth 2.1 ga uklanja) – nikad u produkciji |

Svi korisnici imaju i zadanu ulogu realma `default-roles-akademija` (uloge klijenta `account` – `view-profile`,
`manage-account`). Bez nje se korisnik prijavi, ali korisnička konzola (`/account`) javi „Something went wrong”:
pri uvozu realma iz JSON-a Keycloak zadanu ulogu ne dodjeljuje sam.

Oba klijenta imaju *audience mapper* koji u access token dodaje `aud: medtermin-api`. Uloge su u claimu
`realm_access.roles`, korisničko ime u `preferred_username` – API-ji ih mapiraju u uloge okvira.
Realm ima uključenu zaštitu od pogađanja lozinki (*brute force detection*, 5 pokušaja) i politiku lozinki.

## Kako dobiti token

**Preporučeno (PKCE, kao pravo web sučelje):** IntelliJ HTTP Client (`http-client.env.json`, Auth →
*Authorization Code* + *PKCE*), Postman ili Bruno: Auth URL `http://localhost:8180/realms/akademija/protocol/openid-connect/auth`,
Token URL `.../protocol/openid-connect/token`, Client ID `medtermin-spa`, Redirect `http://localhost:5173/callback`,
PKCE `S256`. Prijava se radi u pregledniku – aplikacija nikad ne vidi lozinku.

**Samo za razvoj (password grant, klijent `medtermin-dev-cli`):**

```bash
TOKEN=$(curl -s -X POST http://localhost:8180/realms/akademija/protocol/openid-connect/token \
  -d grant_type=password -d client_id=medtermin-dev-cli \
  -d username=ana --data-urlencode 'password=Ana12345!' | jq -r .access_token)

curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/rezervacije/moje   # Java
curl -H "Authorization: Bearer $TOKEN" http://localhost:5080/api/rezervacije/moje   # .NET
curl -H "Authorization: Bearer $TOKEN" http://localhost:8000/api/rezervacije/moje   # Python
```

Sadržaj tokena (bez provjere potpisa): `echo $TOKEN | cut -d. -f2 | base64 -d 2>/dev/null | jq` ili jwt.io (samo s testnim tokenima!).

## Automatska provjera

S pokrenutim Keycloakom i API-jima: `./provjera.sh java dotnet python` (ili samo neke od njih) – provjerava 401 bez tokena i s krivotvorenim
potpisom, „samo moje”, 404 za tuđu rezervaciju, 403/200 za ADMIN popis. Isto radi CI (`t3-okviri.yml`).

#!/usr/bin/env bash
# Provjera kompletnog toka: Keycloak (docker compose) -> token -> Java, .NET i/ili Python primjer.
#   tjedan-3/okviri/keycloak/provjera.sh [java] [dotnet] [python]
# Pretpostavlja da su aplikacije već pokrenute (Java na :8080, .NET na :5080, Python na :8000) ili ih pokreće CI.
set -euo pipefail
cd "$(dirname "$0")"
KC=http://localhost:8180/realms/akademija
ciljevi=("$@"); [ ${#ciljevi[@]} -gt 0 ] || ciljevi=(java dotnet python)

echo "Čekam Keycloak …"
for _ in $(seq 1 90); do curl -fsS "$KC/.well-known/openid-configuration" >/dev/null 2>&1 && break; sleep 2; done
curl -fsS "$KC/.well-known/openid-configuration" >/dev/null || { echo "::error::Keycloak nije dostupan"; exit 1; }

token() { # SAMO ZA RAZVOJ: password grant preko klijenta medtermin-dev-cli
  curl -fsS -X POST "$KC/protocol/openid-connect/token" -d grant_type=password -d client_id=medtermin-dev-cli \
    --data-urlencode "username=$1" --data-urlencode "password=$2" | python3 -c 'import json,sys;print(json.load(sys.stdin)["access_token"])'
}
ANA=$(token ana 'Ana12345!'); MARKO=$(token marko 'Marko12345!'); ADMIN=$(token admin 'Admin12345!')
python3 - "$ANA" <<'PY'
import base64, json, sys
p = sys.argv[1].split(".")[1]; p += "=" * (-len(p) % 4)
c = json.loads(base64.urlsafe_b64decode(p))
print("token za ana: iss=%s aud=%s preferred_username=%s realm_access=%s" % (c["iss"], c.get("aud"), c.get("preferred_username"), c.get("realm_access")))
# korisnička konzola (/account) treba zadanu ulogu realma – pri uvozu JSON-a Keycloak je ne dodjeljuje sam
if "manage-account" not in c.get("resource_access", {}).get("account", {}).get("roles", []):
    sys.exit("::error::ana nema ulogu account/manage-account – u realm-akademija.json dodajte default-roles-akademija")
PY

greske=0
ocekuj() { # opis ocekivani_status metoda url [token]
  local opis=$1 ocek=$2 metoda=$3 url=$4 tok=${5:-} st
  if [ -n "$tok" ]; then st=$(curl -s -o /tmp/odg.json -w '%{http_code}' -X "$metoda" -H "Authorization: Bearer $tok" "$url")
  else st=$(curl -s -o /tmp/odg.json -w '%{http_code}' -X "$metoda" "$url"); fi
  if [ "$st" = "$ocek" ]; then echo "  OK   $opis ($st)"; else echo "::error::$opis: očekivano $ocek, dobiveno $st"; greske=$((greske+1)); fi
}
for c in "${ciljevi[@]}"; do
  case $c in
    java) B=http://localhost:8080 ;; dotnet) B=http://localhost:5080 ;; python) B=http://localhost:8000 ;;
    *) echo "nepoznato: $c"; exit 2 ;;
  esac
  echo "== $c ($B)"
  ocekuj "bez tokena -> 401" 401 GET "$B/api/rezervacije/moje"
  ocekuj "neispravan potpis -> 401" 401 GET "$B/api/rezervacije/moje" "${ANA%?????}AAAAA"
  ocekuj "ana: moje rezervacije" 200 GET "$B/api/rezervacije/moje" "$ANA"
  grep -q 'Kontrolni pregled tlaka' /tmp/odg.json && ! grep -q 'Bolovi' /tmp/odg.json \
    && echo "  OK   ana vidi samo svoje" || { echo "::error::$c: ana ne vidi (samo) svoje rezervacije"; greske=$((greske+1)); }
  ocekuj "marko briše aninu -> 404" 404 DELETE "$B/api/rezervacije/1" "$MARKO"
  ocekuj "ana: admin popis -> 403" 403 GET "$B/api/admin/rezervacije" "$ANA"
  ocekuj "admin: admin popis -> 200" 200 GET "$B/api/admin/rezervacije" "$ADMIN"
  ocekuj "admin briše markovu -> 204" 204 DELETE "$B/api/rezervacije/2" "$ADMIN"
done
[ "$greske" -eq 0 ] && echo "Sve provjere prošle." || { echo "Neuspjelih provjera: $greske"; exit 1; }

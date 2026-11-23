namespace Ulaznica.Api.Infrastruktura;

/// <summary>Shema i početni podaci trgovine: događaji, narudžbe i računi.</summary>
public static class PocetniPodaci
{
    public const string Shema = """
        CREATE TABLE dogadjaji (
            id        INTEGER PRIMARY KEY AUTOINCREMENT,
            naziv     TEXT NOT NULL,
            mjesto    TEXT NOT NULL,
            datum     TEXT NOT NULL,
            cijena    NUMERIC NOT NULL,
            kapacitet INTEGER NOT NULL,
            prodano   INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE narudzbe (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            dogadjaj_id     INTEGER NOT NULL REFERENCES dogadjaji(id),
            kupac           TEXT NOT NULL,
            kolicina        INTEGER NOT NULL,
            ime_na_ulaznici TEXT NOT NULL,
            oib             TEXT NOT NULL,
            kreirano        TEXT NOT NULL
        );

        CREATE TABLE racuni (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            narudzba_id INTEGER NOT NULL,
            kupac       TEXT NOT NULL,
            datoteka    TEXT NOT NULL
        );
        """;

    public static void Unesi(Baza baza)
    {
        const string dogadjaj = "INSERT INTO dogadjaji (id, naziv, mjesto, datum, cijena, kapacitet, prodano) VALUES ($id, $n, $m, $d, $c, $k, $p)";
        baza.Izvrsi(dogadjaj, ("$id", 1), ("$n", "Gibonni – akustična turneja"), ("$m", "Lisinski, Zagreb"), ("$d", "2026-12-05T20:00"), ("$c", 35.00m), ("$k", 1800), ("$p", 1200));
        baza.Izvrsi(dogadjaj, ("$id", 2), ("$n", "Hamlet"), ("$m", "HNK Zagreb"), ("$d", "2026-11-28T19:30"), ("$c", 22.00m), ("$k", 700), ("$p", 650));
        baza.Izvrsi(dogadjaj, ("$id", 3), ("$n", "Jazz večer"), ("$m", "Tvornica kulture, Zagreb"), ("$d", "2026-12-10T21:00"), ("$c", 18.00m), ("$k", 400), ("$p", 120));
        baza.Izvrsi(dogadjaj, ("$id", 4), ("$n", "Orašar"), ("$m", "HNK Zagreb"), ("$d", "2026-12-20T18:00"), ("$c", 30.00m), ("$k", 700), ("$p", 300));
        baza.Izvrsi(dogadjaj, ("$id", 5), ("$n", "Zagrebačka filharmonija – zadnja ulaznica"), ("$m", "Lisinski, Zagreb"), ("$d", "2026-12-12T20:00"), ("$c", 25.00m), ("$k", 100), ("$p", 99));
        baza.Izvrsi(dogadjaj, ("$id", 6), ("$n", "Stand-up večer"), ("$m", "Kerempuh, Zagreb"), ("$d", "2026-11-30T20:00"), ("$c", 15.00m), ("$k", 250), ("$p", 250));
        baza.Izvrsi(dogadjaj, ("$id", 7), ("$n", "Dječja predstava: Crvenkapica"), ("$m", "Zagrebačko kazalište lutaka"), ("$d", "2026-12-06T11:00"), ("$c", 8.00m), ("$k", 200), ("$p", 40));
        baza.Izvrsi(dogadjaj, ("$id", 8), ("$n", "Novogodišnji koncert"), ("$m", "Arena Zagreb"), ("$d", "2026-12-31T21:00"), ("$c", 49.00m), ("$k", 10000), ("$p", 5000));

        const string narudzba = "INSERT INTO narudzbe (id, dogadjaj_id, kupac, kolicina, ime_na_ulaznici, oib, kreirano) VALUES ($id, $d, $k, $q, $i, $o, $c)";
        baza.Izvrsi(narudzba, ("$id", 1), ("$d", 1), ("$k", "ana"), ("$q", 2), ("$i", "Ana Anić"), ("$o", "48291736504"), ("$c", "2026-10-30T18:12:00Z"));
        baza.Izvrsi(narudzba, ("$id", 2), ("$d", 2), ("$k", "marko"), ("$q", 1), ("$i", "Marko Marić"), ("$o", "73019485267"), ("$c", "2026-10-31T09:05:00Z"));

        const string racun = "INSERT INTO racuni (id, narudzba_id, kupac, datoteka) VALUES ($id, $n, $k, $d)";
        baza.Izvrsi(racun, ("$id", 1), ("$n", 1), ("$k", "ana"), ("$d", "racun-ana-2026-0001.txt"));
        baza.Izvrsi(racun, ("$id", 2), ("$n", 2), ("$k", "marko"), ("$d", "racun-marko-2026-0002.txt"));
    }

    /// <summary>Datoteke računa koje se pri pokretanju stvaraju u mapi s dokumentima.</summary>
    public static readonly IReadOnlyDictionary<string, string> Dokumenti = new Dictionary<string, string>
    {
        ["racun-ana-2026-0001.txt"] = """
            ULAZNICA d.o.o., Ilica 10, Zagreb
            Račun br. 2026-0001
            Kupac: Ana Anić
            Datum izdavanja: 2026-10-30

            Događaj: Gibonni – akustična turneja (Lisinski, 2026-12-05 20:00)
            Količina: 2 x 35.00 EUR
            Ukupno za platiti: 70.00 EUR
            Način plaćanja: kartica
            """,
        ["racun-marko-2026-0002.txt"] = """
            ULAZNICA d.o.o., Ilica 10, Zagreb
            Račun br. 2026-0002
            Kupac: Marko Marić
            Datum izdavanja: 2026-10-31

            Događaj: Hamlet (HNK Zagreb, 2026-11-28 19:30)
            Količina: 1 x 22.00 EUR
            Ukupno za platiti: 22.00 EUR
            Način plaćanja: kartica
            """,
    };
}

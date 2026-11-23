using System.Collections.Concurrent;

namespace Okviri.Api.Rezervacije;

public sealed record Rezervacija(long Id, string Pacijent, long TerminId, string Napomena);

/// <summary>Rezervacije u memoriji (fokus primjera je sigurnost, ne baza). Seed kao u T2 MedTerminu.</summary>
public sealed class RezervacijeRepozitorij
{
    private readonly ConcurrentDictionary<long, Rezervacija> _zapisi = new();

    public RezervacijeRepozitorij() => VratiPocetnePodatke();

    public void VratiPocetnePodatke()
    {
        _zapisi.Clear();
        foreach (var r in new[]
                 {
                     new Rezervacija(1, "ana", 1, "Kontrolni pregled tlaka"),
                     new Rezervacija(2, "marko", 2, "Bolovi u leđima"),
                     new Rezervacija(3, "ana", 3, "Pregled nalaza krvi"),
                 })
        {
            _zapisi[r.Id] = r;
        }
    }

    public Rezervacija? Pronadi(long id) => _zapisi.GetValueOrDefault(id);

    public IReadOnlyList<Rezervacija> ZaPacijenta(string pacijent) =>
        _zapisi.Values.Where(r => r.Pacijent == pacijent).OrderBy(r => r.Id).ToList();

    public IReadOnlyList<Rezervacija> Sve() => _zapisi.Values.OrderBy(r => r.Id).ToList();

    public bool Obrisi(long id) => _zapisi.TryRemove(id, out _);
}

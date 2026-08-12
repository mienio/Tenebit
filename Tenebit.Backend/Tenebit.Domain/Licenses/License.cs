using Tenebit.Domain.Common;

namespace Tenebit.Domain.Licenses;

public sealed class License
{
    private License() { }

    public License(Guid organizationId, string name, string? vendor, string? licenseKey, int seatsTotal, DateOnly? expiresAt, string? notes)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(name, vendor, licenseKey, seatsTotal, expiresAt, notes);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Vendor { get; private set; }
    public string? LicenseKey { get; private set; }
    public int SeatsTotal { get; private set; }
    public DateOnly? ExpiresAt { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<LicenseSeat> Seats { get; private set; } = [];

    public void Update(string name, string? vendor, string? licenseKey, int seatsTotal, DateOnly? expiresAt, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa licencji jest wymagana.");
        }

        if (seatsTotal < 0)
        {
            throw new DomainException("Liczba miejsc nie może być ujemna.");
        }

        if (seatsTotal < Seats.Count)
        {
            throw new DomainException("Nie można ustawić liczby miejsc poniżej liczby już przypisanych.");
        }

        Name = name.Trim();
        Vendor = string.IsNullOrWhiteSpace(vendor) ? null : vendor.Trim();
        LicenseKey = string.IsNullOrWhiteSpace(licenseKey) ? null : licenseKey.Trim();
        SeatsTotal = seatsTotal;
        ExpiresAt = expiresAt;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void AssignSeat(Guid personId, DateTimeOffset now)
    {
        if (Seats.Any(x => x.PersonId == personId))
        {
            throw new DomainException("Ta osoba ma już przypisane miejsce w tej licencji.");
        }

        if (Seats.Count >= SeatsTotal)
        {
            throw new DomainException("Brak wolnych miejsc w tej licencji.");
        }

        Seats.Add(new LicenseSeat(Id, personId, now));
    }

    public void UnassignSeat(Guid personId)
    {
        Seats.RemoveAll(x => x.PersonId == personId);
    }
}

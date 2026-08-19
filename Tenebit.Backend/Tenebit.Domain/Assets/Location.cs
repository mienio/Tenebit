using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

public sealed class Location
{
    private Location() { }

    public Location(Guid organizationId, string name, string? type, Guid? parentId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
        Update(name, type, parentId, true);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Type { get; private set; } = "Room";
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(string name, string? type, Guid? parentId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa lokalizacji jest wymagana.");
        }

        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Type = string.IsNullOrWhiteSpace(type) ? "Room" : type.Trim();
        ParentId = parentId;
        IsActive = isActive;
    }

    /// <summary>
    /// Zwraca " / "-delimitowaną ścieżkę od korzenia do <paramref name="location"/>. Odporne na cykliczne dane
    /// (np. powstałe przed wprowadzeniem <see cref="WouldCreateCycle"/>) - zamiast zakładać maksymalną głębokość,
    /// zatrzymuje się przy pierwszym powtórzonym węźle.
    /// </summary>
    public static string BuildFullPath(Location location, IReadOnlyDictionary<Guid, Location> byId)
    {
        var segments = new List<string>();
        var visited = new HashSet<Guid>();
        Location? current = location;
        while (current is not null && visited.Add(current.Id))
        {
            segments.Add(current.Name);
            current = current.ParentId.HasValue && byId.TryGetValue(current.ParentId.Value, out var parent) ? parent : null;
        }

        segments.Reverse();
        return string.Join(" / ", segments);
    }

    public static bool WouldCreateCycle(Guid candidateId, Guid newParentId, IReadOnlyDictionary<Guid, Location> byId)
    {
        var visited = new HashSet<Guid>();
        Guid? current = newParentId;
        while (current.HasValue)
        {
            if (current.Value == candidateId || !visited.Add(current.Value))
            {
                return true;
            }

            current = byId.TryGetValue(current.Value, out var node) ? node.ParentId : null;
        }

        return false;
    }
}

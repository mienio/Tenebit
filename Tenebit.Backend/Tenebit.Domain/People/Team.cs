using Tenebit.Domain.Common;

namespace Tenebit.Domain.People;

public sealed class Team
{
    private Team() { }

    public Team(Guid organizationId, string name, Guid? managerId, string? costCenter)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(name, managerId, costCenter);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? ManagerId { get; private set; }
    public string? CostCenter { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(string name, Guid? managerId, string? costCenter)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa zespołu jest wymagana.");
        }

        Name = name.Trim();
        ManagerId = managerId;
        CostCenter = string.IsNullOrWhiteSpace(costCenter) ? null : costCenter.Trim();
    }
}

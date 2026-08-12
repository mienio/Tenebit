using Tenebit.Domain.Common;

namespace Tenebit.Domain.People;

public sealed class PersonRelationType
{
    private PersonRelationType() { }

    public PersonRelationType(Guid organizationId, string name, int sortOrder = 1000)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        SortOrder = sortOrder;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(name);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa typu relacji jest wymagana.");
        }

        Name = name.Trim();
    }
}

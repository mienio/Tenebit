using Tenebit.Domain.Common;

namespace Tenebit.Domain.JobProfiles;

public sealed class JobProfile
{
    private JobProfile() { }

    public JobProfile(Guid organizationId, string name, string? description, Guid? defaultManagerId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(name, description, defaultManagerId);
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? DefaultManagerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<JobProfileAssetCategory> AssetCategories { get; private set; } = [];
    public List<JobProfileProcedure> Procedures { get; private set; } = [];

    public void Update(string name, string? description, Guid? defaultManagerId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Nazwa zestawu stanowiskowego jest wymagana.");
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DefaultManagerId = defaultManagerId;
    }

    public void SetAssetCategories(IEnumerable<Guid> categoryIds)
    {
        AssetCategories.Clear();
        foreach (var categoryId in categoryIds.Distinct()) AssetCategories.Add(new JobProfileAssetCategory(OrganizationId, Id, categoryId));
    }

    public void SetProcedures(IEnumerable<Guid> procedureIds)
    {
        Procedures.Clear();
        foreach (var procedureId in procedureIds.Distinct()) Procedures.Add(new JobProfileProcedure(OrganizationId, Id, procedureId));
    }
}

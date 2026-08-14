using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

public sealed class AssetInspection
{
    private AssetInspection() { }

    public AssetInspection(Guid organizationId, Guid assetId, Guid? assignmentId, DateTimeOffset createdAt, string? createdBy, Guid? offboardingItemId = null)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AssetId = assetId;
        AssignmentId = assignmentId;
        OffboardingItemId = offboardingItemId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public Guid? OffboardingItemId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public bool? SerialNumberMatched { get; private set; }
    public bool? AccessoriesComplete { get; private set; }
    public bool? DataWiped { get; private set; }
    public bool? FunctionalTestPassed { get; private set; }
    public string? DamageAssessmentNotes { get; private set; }
    public InspectionOutcome? Outcome { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public bool IsCompleted => Outcome.HasValue;

    public void Complete(InspectionOutcome outcome, bool serialNumberMatched, bool accessoriesComplete, bool dataWiped, bool functionalTestPassed, string? damageAssessmentNotes, string? notes, DateTimeOffset completedAt, string? completedBy)
    {
        if (IsCompleted) throw new DomainException("Kontrola tego aktywa została już zakończona.");
        Outcome = outcome;
        SerialNumberMatched = serialNumberMatched;
        AccessoriesComplete = accessoriesComplete;
        DataWiped = dataWiped;
        FunctionalTestPassed = functionalTestPassed;
        DamageAssessmentNotes = damageAssessmentNotes;
        Notes = notes;
        CompletedAt = completedAt;
        CompletedBy = completedBy;
    }
}

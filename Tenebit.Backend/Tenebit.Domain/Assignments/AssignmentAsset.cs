namespace Tenebit.Domain.Assignments;

public sealed class AssignmentAsset
{
    private AssignmentAsset() { }

    public AssignmentAsset(Guid assignmentId, Guid assetId, string? issueCondition)
    {
        AssignmentId = assignmentId;
        AssetId = assetId;
        IssueCondition = string.IsNullOrWhiteSpace(issueCondition) ? "Sprawny" : issueCondition.Trim();
    }

    public Guid AssignmentId { get; private set; }
    public Guid AssetId { get; private set; }
    public string IssueCondition { get; private set; } = "Sprawny";
    public string? ReturnCondition { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public string? ReturnLocation { get; private set; }
    public string? ReturnedBy { get; private set; }
    public ReturnResolution? ReturnResolution { get; private set; }
    public string? ReturnNotes { get; private set; }

    public void Resolve(ReturnResolution resolution, DateTimeOffset returnedAt, string? returnCondition, string? returnLocation, string? returnedBy, string? notes)
    {
        ReturnResolution = resolution;
        ReturnedAt = returnedAt;
        ReturnCondition = string.IsNullOrWhiteSpace(returnCondition) ? "Bez uwag" : returnCondition.Trim();
        ReturnLocation = string.IsNullOrWhiteSpace(returnLocation) ? null : returnLocation.Trim();
        ReturnedBy = string.IsNullOrWhiteSpace(returnedBy) ? null : returnedBy.Trim();
        ReturnNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}

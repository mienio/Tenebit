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

    public void SetReturnCondition(string? returnCondition)
    {
        ReturnCondition = string.IsNullOrWhiteSpace(returnCondition) ? "Bez uwag" : returnCondition.Trim();
    }
}

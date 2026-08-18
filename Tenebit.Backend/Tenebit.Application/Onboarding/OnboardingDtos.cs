using Tenebit.Application.Assignments;

namespace Tenebit.Application.Onboarding;

public sealed record OnboardingStepResponse(string Key, string Label, bool Completed, string NextAction);
public sealed record OnboardingStatusResponse(IReadOnlyList<OnboardingStepResponse> Steps, int CompletionPercent);

[ValidatedRequest]
public sealed record CreateStarterPackageRequest(
    string TeamName,
    string EmployeeFirstName,
    string EmployeeLastName,
    string EmployeeEmail,
    string? JobTitle,
    string AssetName,
    string AssetTag,
    string? SerialNumber,
    string CategoryName,
    string? Location,
    string ProcedureTitle,
    string? ProcedureUrl,
    DateOnly? ReturnDueDate);

public sealed record StarterPackageResponse(
    Guid PersonId,
    Guid AssetId,
    Guid ProcedureId,
    Guid AssignmentId,
    string ProtocolNumber,
    string Message);

[ValidatedRequest]
public sealed record CreateEmployeePackageRequest(Guid PersonId, Guid? JobProfileId, IReadOnlyList<Guid> AssetIds, IReadOnlyList<Guid> ProcedureIds, DateOnly? DueDate, string? Notes, IReadOnlyDictionary<string, string>? AssetConditions = null);
public sealed record EmployeePackageResponse(Guid AssignmentId, string ProtocolNumber, AssignmentResponse Assignment, IReadOnlyList<string> Warnings);

public sealed record OnboardingChecklistItemResponse(string Type, Guid ItemId, string Label, string Status, DateTimeOffset? CompletedAt);
public sealed record OnboardingChecklistResponse(Guid PersonId, string PersonName, IReadOnlyList<OnboardingChecklistItemResponse> Items, int CompletedCount, int TotalCount);

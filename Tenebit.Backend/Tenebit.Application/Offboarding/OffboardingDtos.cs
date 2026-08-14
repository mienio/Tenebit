using Tenebit.Domain.Offboarding;

namespace Tenebit.Application.Offboarding;

public sealed record CreateOffboardingCaseRequest(
    Guid PersonId,
    DateTimeOffset EmploymentEndsAt,
    DateTimeOffset ReturnDueDate,
    string? DefaultReturnLocation,
    string? Notes,
    Guid? ProcessOwnerId,
    bool BlockNewReservations,
    bool CancelFutureReservations,
    bool AutoReleaseLicenses);

public sealed record UpdateOffboardingCaseRequest(
    DateTimeOffset EmploymentEndsAt,
    DateTimeOffset ReturnDueDate,
    string? DefaultReturnLocation,
    string? Notes,
    Guid? ProcessOwnerId,
    bool BlockNewReservations,
    bool CancelFutureReservations,
    bool AutoReleaseLicenses);

public sealed record ConfirmOffboardingItemReturnRequest(string? ReturnCondition, string? ReturnLocation, string? Notes);

public sealed record ResolveOffboardingItemRequest(OffboardingItemStatus Status, string Notes);

public sealed record WaiveOffboardingItemRequest(string Reason);

public sealed record CancelOffboardingCaseRequest(string Reason);

public sealed record OffboardingItemResponse(
    Guid Id,
    OffboardingItemType Type,
    Guid? AssetId,
    Guid? AssignmentId,
    Guid? LicenseId,
    string Label,
    bool Required,
    OffboardingItemStatus Status,
    string? EmployeeResponse,
    string? EmployeeComment,
    OffboardingItemAutomationMode AutomationMode,
    DateTimeOffset? AutomationLastAttemptAt,
    string? AutomationError,
    DateTimeOffset? ReceivedAt,
    string? ReceivedBy,
    DateTimeOffset? InspectionCompletedAt,
    string? InspectionCompletedBy,
    string? ResolutionNotes,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    int SortOrder);

public sealed record OffboardingCaseResponse(
    Guid Id,
    Guid PersonId,
    string? PersonName,
    OffboardingCaseStatus Status,
    DateTimeOffset EmploymentEndsAt,
    DateTimeOffset ReturnDueDate,
    string? DefaultReturnLocation,
    string? Notes,
    Guid? ProcessOwnerId,
    bool BlockNewReservations,
    bool CancelFutureReservations,
    bool AutoReleaseLicenses,
    DateTimeOffset? PersonDeactivatedAt,
    DateTimeOffset? ScheduledActionsCompletedAt,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    string? FinalProtocolNumber);

public sealed record OffboardingCaseDetailsResponse(OffboardingCaseResponse Case, IReadOnlyList<OffboardingItemResponse> Items);

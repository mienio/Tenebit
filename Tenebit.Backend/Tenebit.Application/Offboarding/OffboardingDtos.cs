using Tenebit.Application.Reservations;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
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

public sealed record StartOffboardingCaseRequest(bool NotifyEmployee = true);

public sealed record PublicOffboardingItemResponse(Guid Id, string Label, string? AssetTag, OffboardingItemStatus Status, string? EmployeeResponse, string? EmployeeComment, Guid? IssuePhotoEvidenceId);

public sealed record PublicOffboardingResponse(string OrganizationName, DateTimeOffset ReturnDueDate, string? DefaultReturnLocation, string? Notes, IReadOnlyList<PublicOffboardingItemResponse> Items);

public sealed record PublicOffboardingItemAnswer(Guid ItemId, string Response, string? Comment);

public sealed record SubmitPublicOffboardingResponseRequest(IReadOnlyList<PublicOffboardingItemAnswer> Answers);

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

public sealed record OffboardingCaseDetailsResponse(OffboardingCaseResponse Case, IReadOnlyList<OffboardingItemResponse> Items, IReadOnlyList<ReservationResponse> Reservations);

/// <summary>Podsumowanie przed uruchomieniem sprawy (spec 4.5 krok 2) — co osoba trzyma, bez żadnych mutacji.</summary>
public sealed record OffboardingPreviewResponse(
    Guid PersonId,
    string PersonName,
    IReadOnlyList<OffboardingPreviewAssetResponse> HeldAssets,
    IReadOnlyList<OffboardingPreviewAssignmentResponse> OpenAssignments,
    IReadOnlyList<OffboardingPreviewLicenseResponse> LicenseSeats,
    IReadOnlyList<ReservationResponse> Reservations,
    IReadOnlyList<OffboardingPreviewAuditItemResponse> UnresolvedAuditItems);

public sealed record OffboardingPreviewAssetResponse(Guid Id, string Name, string AssetTag, AssetStatus Status);

public sealed record OffboardingPreviewAssignmentResponse(Guid Id, string ProtocolNumber, AssignmentStatus Status, DateTimeOffset IssuedAt);

public sealed record OffboardingPreviewLicenseResponse(Guid Id, string Name);

public sealed record OffboardingPreviewAuditItemResponse(Guid Id, string AssetName, string? AssetTag, string CampaignName, AssetAuditResponse Response);

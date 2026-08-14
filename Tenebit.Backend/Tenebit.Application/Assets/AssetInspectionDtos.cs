using Tenebit.Domain.Assets;

namespace Tenebit.Application.Assets;

public sealed record CompleteAssetInspectionRequest(InspectionOutcome Outcome, bool SerialNumberMatched, bool AccessoriesComplete, bool DataWiped, bool FunctionalTestPassed, string? DamageAssessmentNotes, string? Notes);

public sealed record AssetInspectionResponse(Guid Id, Guid AssetId, Guid? AssignmentId, DateTimeOffset CreatedAt, string? CreatedBy, bool? SerialNumberMatched, bool? AccessoriesComplete, bool? DataWiped, bool? FunctionalTestPassed, string? DamageAssessmentNotes, InspectionOutcome? Outcome, string? Notes, DateTimeOffset? CompletedAt, string? CompletedBy);

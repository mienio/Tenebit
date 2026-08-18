using Tenebit.Domain.Audits;

namespace Tenebit.Application.Audits;

/// <summary>Zakres kampanii — dokładnie jeden z wariantów. Serializowany do <see cref="AssetAuditCampaign.ScopeJson"/>
/// jako migawka historyczna do podglądu; NIE jest używany do zapytań przy przeliczaniu uczestników/pozycji.</summary>
public enum AssetAuditScopeType
{
    Organization,
    Team,
    Location,
    AssetCategory,
    Person
}

public sealed record AssetAuditScope(
    AssetAuditScopeType Type,
    IReadOnlyList<Guid>? TeamIds = null,
    IReadOnlyList<string>? Locations = null,
    IReadOnlyList<Guid>? AssetCategoryIds = null,
    IReadOnlyList<Guid>? PersonIds = null);

[ValidatedRequest]
public sealed record CreateAssetAuditCampaignRequest(string Name, string? Description, DateTimeOffset DueDate, AssetAuditScope Scope);

[ValidatedRequest]
public sealed record UpdateAssetAuditCampaignRequest(string Name, string? Description, DateTimeOffset DueDate, AssetAuditScope Scope);

public sealed record AssetAuditCampaignPreviewResponse(int ParticipantCount, int AssetCount, IReadOnlyList<string> PeopleWithoutEmail);

public sealed record AssetAuditCampaignResponse(
    Guid Id,
    string Name,
    string? Description,
    AssetAuditCampaignStatus Status,
    DateTimeOffset DueDate,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CompletedBy);

public sealed record AssetAuditParticipantResponse(
    Guid Id,
    Guid PersonId,
    string? PersonName,
    string Email,
    AssetAuditParticipantStatus Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? LastReminderAt,
    int ItemCount);

public sealed record AssetAuditItemAdminResponse(
    Guid Id,
    Guid ParticipantId,
    string? ParticipantName,
    Guid AssetId,
    string AssetName,
    string AssetTag,
    string? ExpectedLocation,
    AssetAuditResponse Response,
    string? Comment,
    DateTimeOffset? RespondedAt,
    AssetAuditResolution Resolution,
    string? ResolutionNotes,
    string? ResolvedBy,
    DateTimeOffset? ResolvedAt);

public sealed record AssetAuditCampaignDetailsResponse(
    AssetAuditCampaignResponse Campaign,
    IReadOnlyList<AssetAuditParticipantResponse> Participants,
    IReadOnlyList<AssetAuditItemAdminResponse> Items);

public sealed record PublicAssetAuditItemResponse(Guid Id, string AssetName, string AssetTag, string? Model, AssetAuditResponse Response, string? Comment, Guid? PhotoEvidenceId);

public sealed record PublicAssetAuditResponse(string OrganizationName, string CampaignName, DateTimeOffset DueDate, bool ReadOnly, IReadOnlyList<PublicAssetAuditItemResponse> Items);

[ValidatedRequest]
public sealed record SubmitPublicAssetAuditItemRequest(AssetAuditResponse Response, string? Comment);

[ValidatedRequest]
public sealed record ResolveAssetAuditItemRequest(AssetAuditResolution Resolution, string? Notes, Guid? NewOwnerPersonId);

public sealed record RemindParticipantsResponse(int RemindedCount);

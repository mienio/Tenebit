using Tenebit.Domain.Common;

namespace Tenebit.Domain.Audits;

public sealed class AssetAuditItem
{
    private AssetAuditItem() { }

    public AssetAuditItem(Guid organizationId, Guid campaignId, Guid participantId, Guid assetId,
        Guid expectedPersonId, string? expectedLocation)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CampaignId = campaignId;
        ParticipantId = participantId;
        AssetId = assetId;
        ExpectedPersonId = expectedPersonId;
        ExpectedLocation = string.IsNullOrWhiteSpace(expectedLocation) ? null : expectedLocation.Trim();
        Response = AssetAuditResponse.Pending;
        Resolution = AssetAuditResolution.None;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid ExpectedPersonId { get; private set; }
    public string? ExpectedLocation { get; private set; }
    public AssetAuditResponse Response { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public AssetAuditResolution Resolution { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }

    /// <summary>Blokada "już wysłano odpowiedzi" celowo NIE jest duplikowana tutaj - sprawdzana jest wyłącznie
    /// przez serwis na podstawie <see cref="AssetAuditParticipant.Status"/>, żeby nie utrzymywać dwóch niezależnych
    /// blokad, które mogłyby się rozjechać.</summary>
    public void RecordResponse(AssetAuditResponse response, string? comment, DateTimeOffset respondedAt)
    {
        Response = response;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        RespondedAt = respondedAt;
    }

    /// <summary>Rozstrzygnięcie jest jednorazowe - korekta wcześniejszego rozstrzygnięcia to logika poza zakresem
    /// tego kroku.</summary>
    public void Resolve(AssetAuditResolution resolution, string? notes, string resolvedBy, DateTimeOffset resolvedAt)
    {
        if (resolution == AssetAuditResolution.None)
        {
            throw new DomainException("Rozstrzygnięcie musi być inne niż None.");
        }

        if (Resolution != AssetAuditResolution.None)
        {
            throw new DomainException("Pozycja jest już rozstrzygnięta.");
        }

        var requiresNotes = resolution is AssetAuditResolution.AssetMarkedLost
            or AssetAuditResolution.AssetMarkedDamaged
            or AssetAuditResolution.OwnershipCorrected;

        if (requiresNotes && string.IsNullOrWhiteSpace(notes))
        {
            throw new DomainException("Uzasadnienie jest wymagane dla tego rozstrzygnięcia.");
        }

        if (string.IsNullOrWhiteSpace(resolvedBy))
        {
            throw new DomainException("Osoba rozstrzygająca jest wymagana.");
        }

        Resolution = resolution;
        ResolutionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ResolvedBy = resolvedBy.Trim();
        ResolvedAt = resolvedAt;
    }
}

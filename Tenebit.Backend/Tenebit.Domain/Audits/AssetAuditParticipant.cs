using Tenebit.Domain.Common;

namespace Tenebit.Domain.Audits;

public sealed class AssetAuditParticipant
{
    private AssetAuditParticipant() { }

    public AssetAuditParticipant(Guid organizationId, Guid campaignId, Guid personId, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Adres e-mail uczestnika jest wymagany.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CampaignId = campaignId;
        PersonId = personId;
        Email = email.Trim();
        Status = AssetAuditParticipantStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid PersonId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? TokenHash { get; private set; }
    public DateTimeOffset? TokenExpiresAt { get; private set; }
    public DateTimeOffset? TokenRevokedAt { get; private set; }
    public AssetAuditParticipantStatus Status { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? LastReminderAt { get; private set; }

    public void SetToken(string tokenHash, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Hash tokenu jest wymagany.");
        }

        TokenHash = tokenHash.Trim();
        TokenExpiresAt = expiresAt;
        TokenRevokedAt = null;
    }

    public void RevokeToken(DateTimeOffset revokedAt)
    {
        TokenRevokedAt ??= revokedAt;
    }

    public void MarkInProgress()
    {
        if (Status == AssetAuditParticipantStatus.Pending)
        {
            Status = AssetAuditParticipantStatus.InProgress;
        }
    }

    /// <summary>Po wysłaniu odpowiedzi pracownik nie może już nic zmienić samodzielnie - ponowne otwarcie
    /// jest możliwe wyłącznie przez administratora (<see cref="Reopen"/>), sekcja 5.5.</summary>
    public void Submit(DateTimeOffset submittedAt)
    {
        if (Status is AssetAuditParticipantStatus.Submitted or AssetAuditParticipantStatus.Reviewed)
        {
            throw new DomainException("Odpowiedzi zostały już wysłane.");
        }

        Status = AssetAuditParticipantStatus.Submitted;
        SubmittedAt = submittedAt;
    }

    /// <summary>Wywoływane wyłącznie przez administratora (kontrola uprawnień w serwisie) - nie da się otworzyć
    /// ponownie odpowiedzi, które nigdy nie zostały wysłane.</summary>
    public void Reopen(DateTimeOffset reopenedAt)
    {
        if (Status != AssetAuditParticipantStatus.Submitted)
        {
            throw new DomainException("Ponowne otwarcie jest możliwe tylko dla wysłanych odpowiedzi.");
        }

        Status = AssetAuditParticipantStatus.InProgress;
    }

    public void MarkReminded(DateTimeOffset at)
    {
        LastReminderAt = at;
    }
}

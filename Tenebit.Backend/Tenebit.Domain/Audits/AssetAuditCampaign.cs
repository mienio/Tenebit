using Tenebit.Domain.Common;

namespace Tenebit.Domain.Audits;

public sealed class AssetAuditCampaign
{
    private AssetAuditCampaign() { }

    public AssetAuditCampaign(Guid organizationId, string name, string? description, DateTimeOffset dueDate,
        string? scopeJson, string createdBy, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa kampanii jest wymagana.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
        ScopeJson = scopeJson;
        CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy.Trim();
        CreatedAt = createdAt;
        Status = AssetAuditCampaignStatus.Draft;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public AssetAuditCampaignStatus Status { get; private set; }
    public DateTimeOffset DueDate { get; private set; }

    /// <summary>Migawka definicji zakresu kampanii, wyłącznie do historycznego podglądu - nie jest używana do zapytań.</summary>
    public string? ScopeJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }

    public void Start(DateTimeOffset startedAt)
    {
        if (Status != AssetAuditCampaignStatus.Draft)
        {
            throw new DomainException("Kampanię można uruchomić tylko ze statusu roboczego.");
        }

        Status = AssetAuditCampaignStatus.Active;
        StartedAt ??= startedAt;
    }

    /// <summary>Przechodzi do Reviewing gdy wszyscy uczestnicy odpowiedzieli (Submitted/Reviewed), a kampania jest Active.
    /// W innym przypadku nie zmienia statusu.</summary>
    public void RecomputeStatus(IReadOnlyCollection<AssetAuditParticipant> participants)
    {
        if (Status != AssetAuditCampaignStatus.Active)
        {
            return;
        }

        var allResponded = participants.Count > 0 && participants.All(p =>
            p.Status is AssetAuditParticipantStatus.Submitted or AssetAuditParticipantStatus.Reviewed);

        if (allResponded)
        {
            Status = AssetAuditCampaignStatus.Reviewing;
        }
    }

    /// <summary>Idempotentny. Dozwolony z Active albo Reviewing - administrator może jawnie zakończyć kampanię
    /// z nieudzielonymi odpowiedziami (sekcja 5.7).</summary>
    public void Complete(DateTimeOffset completedAt, string completedBy)
    {
        if (Status == AssetAuditCampaignStatus.Completed)
        {
            return;
        }

        if (Status is not (AssetAuditCampaignStatus.Active or AssetAuditCampaignStatus.Reviewing))
        {
            throw new DomainException("Kampanię można zakończyć tylko ze statusu aktywnego albo w przeglądzie.");
        }

        Status = AssetAuditCampaignStatus.Completed;
        CompletedAt = completedAt;
        CompletedBy = string.IsNullOrWhiteSpace(completedBy) ? "system" : completedBy.Trim();
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status == AssetAuditCampaignStatus.Cancelled)
        {
            return;
        }

        if (Status == AssetAuditCampaignStatus.Completed)
        {
            throw new DomainException("Nie można anulować zakończonej kampanii.");
        }

        Status = AssetAuditCampaignStatus.Cancelled;
    }

    /// <summary>Zakres kampanii po uruchomieniu jest zablokowany (sekcja 5.4) - jedyną dozwoloną zmianą jest
    /// wydłużenie terminu, żeby nie utracić już zebranych odpowiedzi.</summary>
    public void ExtendDueDate(DateTimeOffset newDueDate)
    {
        if (newDueDate <= DueDate)
        {
            throw new DomainException("Nowy termin musi być późniejszy niż obecny.");
        }

        DueDate = newDueDate;
    }

    /// <summary>Edycja nazwy/opisu/terminu/zakresu dozwolona wyłącznie w Draft - po starcie zakres jest zablokowany
    /// (sekcja 5.4), a wydłużenie terminu po starcie idzie przez <see cref="ExtendDueDate"/>.</summary>
    public void UpdateDraft(string name, string? description, DateTimeOffset dueDate, string? scopeJson)
    {
        if (Status != AssetAuditCampaignStatus.Draft)
        {
            throw new DomainException("Kampanię można edytować tylko w statusie roboczym.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa kampanii jest wymagana.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
        ScopeJson = scopeJson;
    }
}

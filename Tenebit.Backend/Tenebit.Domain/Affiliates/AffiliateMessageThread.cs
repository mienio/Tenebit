using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

public enum AffiliateMessageThreadStatus
{
    Open,
    Closed
}

/// <summary>Lets the admin's inbox tell "how do I get more clicks" apart from a payout dispute at a
/// glance, and lets the "Zgłoś zastrzeżenie" (report a grievance) button in the partner panel create a
/// visibly different thread from an ordinary question - it does not gate anything technically, both
/// kinds share the same reply/close flow.</summary>
public enum AffiliateMessageThreadCategory
{
    General,
    Complaint
}

/// <summary>
/// A conversation between one affiliate and the admin - deliberately not a full helpdesk, just enough
/// structure (subject + category + unread badges for both sides) to support the brief's ask for "some
/// contact form that reaches me and I can answer back". One affiliate can have several threads (e.g. a
/// payout question kept separate from a grievance).
/// </summary>
public sealed class AffiliateMessageThread
{
    private AffiliateMessageThread() { }

    public AffiliateMessageThread(Guid affiliateId, string subject, AffiliateMessageThreadCategory category, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new DomainException("Temat wiadomości jest wymagany.");

        Id = Guid.NewGuid();
        AffiliateId = affiliateId;
        Subject = subject.Trim();
        Category = category;
        Status = AffiliateMessageThreadStatus.Open;
        CreatedAt = now;
        LastMessageAt = now;
        UnreadByAdmin = true;
        UnreadByAffiliate = false;
    }

    public Guid Id { get; private set; }
    public Guid AffiliateId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public AffiliateMessageThreadCategory Category { get; private set; }
    public AffiliateMessageThreadStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }
    public bool UnreadByAdmin { get; private set; }
    public bool UnreadByAffiliate { get; private set; }

    public void RecordAffiliateReply(DateTimeOffset now)
    {
        LastMessageAt = now;
        UnreadByAdmin = true;
        if (Status == AffiliateMessageThreadStatus.Closed) Status = AffiliateMessageThreadStatus.Open;
    }

    public void RecordAdminReply(DateTimeOffset now)
    {
        LastMessageAt = now;
        UnreadByAffiliate = true;
    }

    public void MarkReadByAdmin() => UnreadByAdmin = false;
    public void MarkReadByAffiliate() => UnreadByAffiliate = false;

    public void Close() => Status = AffiliateMessageThreadStatus.Closed;
    public void Reopen() => Status = AffiliateMessageThreadStatus.Open;
}

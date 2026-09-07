using Tenebit.Domain.Common;

namespace Tenebit.Domain.Affiliates;

public enum AffiliateMessageSenderType
{
    Affiliate,
    Admin
}

/// <summary>
/// One message inside an <see cref="AffiliateMessageThread"/>. <see cref="Body"/> must always be
/// rendered as plain text on both ends (never HTML/markdown) - the admin panel has high privileges, so
/// a free-text field an affiliate controls is a classic stored-XSS vector if it were ever rendered
/// unescaped (spec §12.4).
/// </summary>
public sealed class AffiliateMessage
{
    public const int MaxBodyLength = 5000;

    private AffiliateMessage() { }

    public AffiliateMessage(Guid threadId, AffiliateMessageSenderType senderType, string body, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("Treść wiadomości jest wymagana.");
        if (body.Length > MaxBodyLength) throw new DomainException($"Wiadomość może mieć maksymalnie {MaxBodyLength} znaków.");

        Id = Guid.NewGuid();
        ThreadId = threadId;
        SenderType = senderType;
        Body = body.Trim();
        SentAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ThreadId { get; private set; }
    public AffiliateMessageSenderType SenderType { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
}

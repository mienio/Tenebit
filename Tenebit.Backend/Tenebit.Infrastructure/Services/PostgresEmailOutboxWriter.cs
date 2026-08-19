using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

public sealed class PostgresEmailOutboxWriter : IEmailOutboxWriter
{
    private const int MaxRecipientLength = 320;
    private const int MaxSubjectLength = 500;
    private const int MaxHtmlLength = 250_000;
    private const int MaxPurposeLength = 80;
    private const int MaxIdempotencyKeyLength = 160;

    private readonly TenebitDbContext _db;
    private readonly IFieldEncryptor _fieldEncryptor;
    private readonly IClock _clock;

    public PostgresEmailOutboxWriter(TenebitDbContext db, IFieldEncryptor fieldEncryptor, IClock clock)
    {
        _db = db;
        _fieldEncryptor = fieldEncryptor;
        _clock = clock;
    }

    public async Task EnqueueAsync(
        Guid organizationId,
        string recipient,
        string subject,
        string htmlBody,
        string purpose,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        recipient = recipient.Trim();
        subject = subject.Trim();
        purpose = purpose.Trim();
        idempotencyKey = idempotencyKey.Trim();

        if (recipient.Length > MaxRecipientLength) throw new ArgumentOutOfRangeException(nameof(recipient));
        if (subject.Length > MaxSubjectLength) throw new ArgumentOutOfRangeException(nameof(subject));
        if (htmlBody.Length > MaxHtmlLength) throw new ArgumentOutOfRangeException(nameof(htmlBody));
        if (purpose.Length > MaxPurposeLength) throw new ArgumentOutOfRangeException(nameof(purpose));
        if (idempotencyKey.Length > MaxIdempotencyKeyLength) throw new ArgumentOutOfRangeException(nameof(idempotencyKey));

        var id = Guid.NewGuid();
        var recipientCiphertext = _fieldEncryptor.Encrypt("email-outbox-recipient", recipient);
        var subjectCiphertext = _fieldEncryptor.Encrypt("email-outbox-subject", subject);
        var htmlCiphertext = _fieldEncryptor.Encrypt("email-outbox-html", htmlBody);
        var createdAt = _clock.UtcNow;

        // Deliberately raw SQL: the insert participates in the DbContext's current transaction while avoiding
        // raw capability-bearing bodies in the EF change tracker. The unique key makes execution-strategy retries
        // idempotent without storing the raw token as a deduplication value.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO tenebit.email_outbox_messages
                ("Id", "OrganizationId", "RecipientCiphertext", "SubjectCiphertext", "HtmlCiphertext",
                 "Purpose", "IdempotencyKey", "CreatedAt", "AttemptCount")
            VALUES
                ({id}, {organizationId}, {recipientCiphertext}, {subjectCiphertext}, {htmlCiphertext},
                 {purpose}, {idempotencyKey}, {createdAt}, 0)
            ON CONFLICT ("OrganizationId", "IdempotencyKey") DO NOTHING;
            """,
            cancellationToken);
    }
}

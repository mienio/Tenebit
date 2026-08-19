using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

/// <summary>
/// Durable, multi-replica-safe e-mail dispatcher. Claim transactions are short and use FOR UPDATE SKIP LOCKED;
/// SMTP happens after commit. A crash after SMTP but before SentAt may cause an at-least-once retry, therefore
/// every attempt uses a stable RFC Message-Id derived from the outbox row ID so downstream MTAs can de-duplicate.
/// </summary>
public sealed class EmailOutboxBackgroundService : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 8;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IEmailTransport _transport;
    private readonly ILogger<EmailOutboxBackgroundService> _logger;

    public EmailOutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IEmailTransport transport,
        ILogger<EmailOutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _transport = transport;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Email:OutboxDispatcherEnabled", true))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DispatchBatchAsync(stoppingToken);
                if (processed == 0)
                    await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SecurityTelemetry.BackgroundJobFailure();
                _logger.LogError(ex, "Email outbox dispatcher iteration failed.");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        var fieldEncryptor = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var leaseId = Guid.NewGuid();
        var messages = await ClaimBatchAsync(db, leaseId, now, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var recipient = fieldEncryptor.Decrypt("email-outbox-recipient", message.RecipientCiphertext);
                var subject = fieldEncryptor.Decrypt("email-outbox-subject", message.SubjectCiphertext);
                var html = fieldEncryptor.Decrypt("email-outbox-html", message.HtmlCiphertext);
                var stableMessageId = $"tenebit-{message.Id:N}";

                await _transport.SendAsync(recipient, subject, html, stableMessageId, cancellationToken);
                await MarkSentAsync(db, message.Id, leaseId, clock.UtcNow, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                SecurityTelemetry.BackgroundJobFailure();
                var errorCode = ex.GetType().Name;
                if (message.AttemptCount >= MaxAttempts)
                {
                    await MarkDeadLetterAsync(db, message.Id, leaseId, errorCode, cancellationToken);
                    SecurityTelemetry.EmailOutboxDeadLetter();
                    _logger.LogError("Email outbox message {MessageId} exhausted delivery attempts and was dead-lettered with its encrypted payload erased.", message.Id);
                }
                else
                {
                    var delay = RetryDelay(message.AttemptCount);
                    await MarkFailedAsync(db, message.Id, leaseId, clock.UtcNow, delay, errorCode, cancellationToken);
                    _logger.LogWarning("Email outbox message {MessageId} delivery failed on attempt {AttemptCount}; retry scheduled.", message.Id, message.AttemptCount);
                }
            }
        }

        return messages.Count;
    }

    private static TimeSpan RetryDelay(int attemptCount)
    {
        var minutes = Math.Min(360, Math.Pow(2, Math.Clamp(attemptCount - 1, 0, 8)));
        return TimeSpan.FromMinutes(minutes);
    }

    private static async Task<List<ClaimedEmail>> ClaimBatchAsync(
        TenebitDbContext db,
        Guid leaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var leaseUntil = now + LeaseDuration;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            WITH candidates AS (
                SELECT "Id"
                FROM tenebit.email_outbox_messages
                WHERE "SentAt" IS NULL
                  AND "AttemptCount" < @maxAttempts
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= @now)
                  AND ("LeaseUntil" IS NULL OR "LeaseUntil" <= @now)
                ORDER BY "CreatedAt", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT @batchSize
            )
            UPDATE tenebit.email_outbox_messages AS m
            SET "LeaseId" = @leaseId,
                "LeaseUntil" = @leaseUntil,
                "AttemptCount" = m."AttemptCount" + 1
            FROM candidates AS c
            WHERE m."Id" = c."Id"
            RETURNING m."Id", m."RecipientCiphertext", m."SubjectCiphertext", m."HtmlCiphertext", m."AttemptCount";
            """;
        AddParameter(command, "maxAttempts", MaxAttempts);
        AddParameter(command, "now", now);
        AddParameter(command, "batchSize", BatchSize);
        AddParameter(command, "leaseId", leaseId);
        AddParameter(command, "leaseUntil", leaseUntil);

        var result = new List<ClaimedEmail>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new ClaimedEmail(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task MarkSentAsync(
        TenebitDbContext db,
        Guid id,
        Guid leaseId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE tenebit.email_outbox_messages
            SET "SentAt" = {sentAt}, "LeaseId" = NULL, "LeaseUntil" = NULL,
                "NextAttemptAt" = NULL, "LastError" = NULL,
                "RecipientCiphertext" = '', "SubjectCiphertext" = '', "HtmlCiphertext" = ''
            WHERE "Id" = {id} AND "LeaseId" = {leaseId} AND "SentAt" IS NULL;
            """,
            cancellationToken);
    }

    private static async Task MarkFailedAsync(
        TenebitDbContext db,
        Guid id,
        Guid leaseId,
        DateTimeOffset attemptedAt,
        TimeSpan retryDelay,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var nextAttemptAt = attemptedAt + retryDelay;
        if (errorCode.Length > 80) errorCode = errorCode[..80];
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE tenebit.email_outbox_messages
            SET "LeaseId" = NULL, "LeaseUntil" = NULL,
                "NextAttemptAt" = {nextAttemptAt}, "LastError" = {errorCode}
            WHERE "Id" = {id} AND "LeaseId" = {leaseId} AND "SentAt" IS NULL;
            """,
            cancellationToken);
    }


    private static async Task MarkDeadLetterAsync(
        TenebitDbContext db,
        Guid id,
        Guid leaseId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (errorCode.Length > 80) errorCode = errorCode[..80];
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE tenebit.email_outbox_messages
            SET "LeaseId" = NULL, "LeaseUntil" = NULL,
                "NextAttemptAt" = NULL, "LastError" = {errorCode},
                "RecipientCiphertext" = '', "SubjectCiphertext" = '', "HtmlCiphertext" = ''
            WHERE "Id" = {id} AND "LeaseId" = {leaseId} AND "SentAt" IS NULL;
            """,
            cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ClaimedEmail(
        Guid Id,
        string RecipientCiphertext,
        string SubjectCiphertext,
        string HtmlCiphertext,
        int AttemptCount);
}

using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class EmailOutboxIntegrationTests : IClassFixture<TenebitApiFactory>
{
    private readonly TenebitApiFactory _factory;

    public EmailOutboxIntegrationTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Enqueue_RollsBackWithBusinessTransaction_AndEncryptsSecretAtRest()
    {
        await ResetOutboxAsync();
        var (organization, _, _) = await _factory.SeedTenantAsync("OutboxAtomic", "owner");
        var secret = $"RAW-OUTBOX-SECRET-{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IEmailOutboxWriter>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.ExecuteInTransactionAsync<bool>(async ct =>
            {
                await writer.EnqueueAsync(organization.Id, "recipient@example.test", "subject", $"<p>{secret}</p>", "test", "rollback-key", ct);
                throw new InvalidOperationException("fault injection after enqueue");
            }, CancellationToken.None));
        }

        Assert.Equal(0, await CountOutboxAsync(organization.Id));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IEmailOutboxWriter>();
            await db.ExecuteInTransactionAsync(async ct =>
            {
                await writer.EnqueueAsync(organization.Id, "recipient@example.test", "subject", $"<p>{secret}</p>", "test", "commit-key", ct);
                await writer.EnqueueAsync(organization.Id, "recipient@example.test", "subject", $"<p>{secret}</p>", "test", "commit-key", ct);
                return true;
            }, CancellationToken.None);
        }

        var stored = await ReadSingleOutboxAsync(organization.Id);
        Assert.Equal(1, stored.Count);
        Assert.DoesNotContain(secret, stored.RecipientCiphertext, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, stored.SubjectCiphertext, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, stored.HtmlCiphertext, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoDispatchers_ClaimSamePendingMessage_ExactlyOneTransportSendOccurs()
    {
        await ResetOutboxAsync();
        var (organization, _, _) = await _factory.SeedTenantAsync("OutboxWorkers", "owner");
        await EnqueueAsync(organization.Id, "worker-key");

        var transport = new RecordingTransport(delay: TimeSpan.FromMilliseconds(150));
        var first = CreateDispatcher(transport);
        var second = CreateDispatcher(transport);

        var processed = await Task.WhenAll(
            first.DispatchBatchAsync(CancellationToken.None),
            second.DispatchBatchAsync(CancellationToken.None));

        Assert.Equal(1, processed.Sum());
        Assert.Single(transport.MessageIds);
        var row = await ReadSingleOutboxAsync(organization.Id);
        Assert.True(row.IsSent);
        Assert.Equal(string.Empty, row.RecipientCiphertext);
        Assert.Equal(string.Empty, row.SubjectCiphertext);
        Assert.Equal(string.Empty, row.HtmlCiphertext);
        Assert.Equal(1, row.AttemptCount);
    }

    [Fact]
    public async Task FailedDelivery_IsRetried_WithStableMessageId()
    {
        await ResetOutboxAsync();
        var (organization, _, _) = await _factory.SeedTenantAsync("OutboxRetry", "owner");
        await EnqueueAsync(organization.Id, "retry-key");

        var transport = new RecordingTransport(failuresBeforeSuccess: 1);
        var dispatcher = CreateDispatcher(transport);

        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));
        var failed = await ReadSingleOutboxAsync(organization.Id);
        Assert.False(failed.IsSent);
        Assert.Equal(1, failed.AttemptCount);
        Assert.True(failed.HasNextAttempt);
        Assert.Equal(nameof(InvalidOperationException), failed.LastError);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenebit.email_outbox_messages SET \"NextAttemptAt\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"OrganizationId\" = {organization.Id}");
        }

        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));
        var sent = await ReadSingleOutboxAsync(organization.Id);
        Assert.True(sent.IsSent);
        Assert.Equal(string.Empty, sent.HtmlCiphertext);
        Assert.Equal(2, sent.AttemptCount);
        Assert.Equal(2, transport.MessageIds.Count);
        Assert.Single(transport.MessageIds.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ExpiredLeaseAfterSimulatedPostSmtpCrash_RetriesWithSameStableMessageId()
    {
        await ResetOutboxAsync();
        var (organization, _, _) = await _factory.SeedTenantAsync("OutboxCrash", "owner");
        await EnqueueAsync(organization.Id, "crash-key");
        var id = await ReadSingleOutboxIdAsync(organization.Id);
        var firstAttemptMessageId = $"tenebit-{id:N}";

        // Simulate: worker claimed row, SMTP accepted the message, then process died before SentAt update.
        // When the lease expires another worker must retry with the same stable RFC Message-Id.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenebit.email_outbox_messages SET \"AttemptCount\" = 1, \"LeaseId\" = {Guid.NewGuid()}, \"LeaseUntil\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"Id\" = {id}");
        }

        var transport = new RecordingTransport();
        var dispatcher = CreateDispatcher(transport);
        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));

        Assert.Single(transport.MessageIds);
        Assert.Equal(firstAttemptMessageId, transport.MessageIds.Single());
        var row = await ReadSingleOutboxAsync(organization.Id);
        Assert.True(row.IsSent);
        Assert.Equal(2, row.AttemptCount);
    }

    [Fact]
    public async Task ExhaustedDelivery_DeadLettersAndErasesEncryptedPayload()
    {
        await ResetOutboxAsync();
        var (organization, _, _) = await _factory.SeedTenantAsync("OutboxDeadLetter", "owner");
        await EnqueueAsync(organization.Id, "dead-letter-key");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenebit.email_outbox_messages SET \"AttemptCount\" = 7, \"NextAttemptAt\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"OrganizationId\" = {organization.Id}");
        }

        var transport = new RecordingTransport(failuresBeforeSuccess: 10);
        var dispatcher = CreateDispatcher(transport);

        Assert.Equal(1, await dispatcher.DispatchBatchAsync(CancellationToken.None));
        var deadLetter = await ReadSingleOutboxAsync(organization.Id);
        Assert.False(deadLetter.IsSent);
        Assert.Equal(8, deadLetter.AttemptCount);
        Assert.False(deadLetter.HasNextAttempt);
        Assert.Equal(nameof(InvalidOperationException), deadLetter.LastError);
        Assert.Equal(string.Empty, deadLetter.RecipientCiphertext);
        Assert.Equal(string.Empty, deadLetter.SubjectCiphertext);
        Assert.Equal(string.Empty, deadLetter.HtmlCiphertext);

        Assert.Equal(0, await dispatcher.DispatchBatchAsync(CancellationToken.None));
        Assert.Single(transport.MessageIds);
    }

    private EmailOutboxBackgroundService CreateDispatcher(IEmailTransport transport) => new(
        _factory.Services.GetRequiredService<IServiceScopeFactory>(),
        _factory.Services.GetRequiredService<IConfiguration>(),
        transport,
        NullLogger<EmailOutboxBackgroundService>.Instance);

    private async Task EnqueueAsync(Guid organizationId, string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IEmailOutboxWriter>();
        await db.ExecuteInTransactionAsync(async ct =>
        {
            await writer.EnqueueAsync(organizationId, "recipient@example.test", "subject", "<p>body</p>", "integration-test", key, ct);
            return true;
        }, CancellationToken.None);
    }

    private async Task ResetOutboxAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE tenebit.email_outbox_messages");
    }

    private async Task<int> CountOutboxAsync(Guid organizationId)
    {
        var row = await ReadOutboxAsync(organizationId);
        return row.Count;
    }

    private async Task<Guid> ReadSingleOutboxIdAsync(Guid organizationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT \"Id\" FROM tenebit.email_outbox_messages WHERE \"OrganizationId\" = @organizationId LIMIT 1";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "organizationId";
            parameter.Value = organizationId;
            command.Parameters.Add(parameter);
            var value = await command.ExecuteScalarAsync();
            return Assert.IsType<Guid>(value);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<OutboxProbe> ReadSingleOutboxAsync(Guid organizationId)
    {
        var row = await ReadOutboxAsync(organizationId);
        Assert.Equal(1, row.Count);
        return row;
    }

    private async Task<OutboxProbe> ReadOutboxAsync(Guid organizationId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)::int,
                       COALESCE(MAX("RecipientCiphertext"), ''),
                       COALESCE(MAX("SubjectCiphertext"), ''),
                       COALESCE(MAX("HtmlCiphertext"), ''),
                       COALESCE(BOOL_OR("SentAt" IS NOT NULL), false),
                       COALESCE(MAX("AttemptCount"), 0),
                       COALESCE(BOOL_OR("NextAttemptAt" IS NOT NULL), false),
                       MAX("LastError")
                FROM tenebit.email_outbox_messages
                WHERE "OrganizationId" = @organizationId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "organizationId";
            parameter.Value = organizationId;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return new OutboxProbe(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetInt32(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetString(7));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private sealed record OutboxProbe(
        int Count,
        string RecipientCiphertext,
        string SubjectCiphertext,
        string HtmlCiphertext,
        bool IsSent,
        int AttemptCount,
        bool HasNextAttempt,
        string? LastError);

    private sealed class RecordingTransport : IEmailTransport
    {
        private readonly TimeSpan _delay;
        private int _failuresRemaining;
        public ConcurrentBag<string> MessageIds { get; } = [];

        public RecordingTransport(int failuresBeforeSuccess = 0, TimeSpan? delay = null)
        {
            _failuresRemaining = failuresBeforeSuccess;
            _delay = delay ?? TimeSpan.Zero;
        }

        public async Task SendAsync(string to, string subject, string htmlBody, string messageId, CancellationToken cancellationToken)
        {
            MessageIds.Add(messageId);
            if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken);
            if (Interlocked.Decrement(ref _failuresRemaining) >= 0)
                throw new InvalidOperationException("simulated SMTP timeout");
        }
    }
}

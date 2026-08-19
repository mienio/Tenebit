using System.Diagnostics.Metrics;
using System.Threading;

namespace Tenebit.Application.Common;

/// <summary>Low-cardinality security counters. Never attach user IDs, e-mail addresses, tokens or tenant names.</summary>
public static class SecurityTelemetry
{
    private static readonly Meter Meter = new("Tenebit.Security", "1.0");
    private static readonly Counter<long> OAuthRejectedCounter = Meter.CreateCounter<long>("tenebit.security.oauth.rejected");
    private static readonly Counter<long> TwoFactorRejectedCounter = Meter.CreateCounter<long>("tenebit.security.2fa.rejected");
    private static readonly Counter<long> RefreshReuseCounter = Meter.CreateCounter<long>("tenebit.security.refresh.reuse");
    private static readonly Counter<long> WebhookRejectedCounter = Meter.CreateCounter<long>("tenebit.security.stripe.webhook_rejected");
    private static readonly Counter<long> EncryptionFailureCounter = Meter.CreateCounter<long>("tenebit.security.encryption.decrypt_failure");
    private static readonly Counter<long> ReconciliationFailureCounter = Meter.CreateCounter<long>("tenebit.security.stripe.reconciliation_failure");
    private static readonly Counter<long> AuthorizationDeniedCounter = Meter.CreateCounter<long>("tenebit.security.authorization.denied");
    private static readonly Counter<long> PublicTokenRejectedCounter = Meter.CreateCounter<long>("tenebit.security.public_token.rejected");
    private static readonly Counter<long> BackgroundJobFailureCounter = Meter.CreateCounter<long>("tenebit.security.background_job.failure");
    private static readonly Counter<long> EmailOutboxDeadLetterCounter = Meter.CreateCounter<long>("tenebit.security.email_outbox.dead_letter");

    private static long _oauthRejected, _twoFactorRejected, _refreshReuse, _webhookRejected, _encryptionFailure, _reconciliationFailure, _authorizationDenied, _publicTokenRejected, _backgroundJobFailure, _emailOutboxDeadLetter;

    public static void OAuthRejected() { Interlocked.Increment(ref _oauthRejected); OAuthRejectedCounter.Add(1); }
    public static void TwoFactorRejected() { Interlocked.Increment(ref _twoFactorRejected); TwoFactorRejectedCounter.Add(1); }
    public static void RefreshReuse() { Interlocked.Increment(ref _refreshReuse); RefreshReuseCounter.Add(1); }
    public static void WebhookRejected() { Interlocked.Increment(ref _webhookRejected); WebhookRejectedCounter.Add(1); }
    public static void EncryptionFailure() { Interlocked.Increment(ref _encryptionFailure); EncryptionFailureCounter.Add(1); }
    public static void ReconciliationFailure() { Interlocked.Increment(ref _reconciliationFailure); ReconciliationFailureCounter.Add(1); }
    public static void AuthorizationDenied() { Interlocked.Increment(ref _authorizationDenied); AuthorizationDeniedCounter.Add(1); }
    public static void PublicTokenRejected() { Interlocked.Increment(ref _publicTokenRejected); PublicTokenRejectedCounter.Add(1); }
    public static void BackgroundJobFailure() { Interlocked.Increment(ref _backgroundJobFailure); BackgroundJobFailureCounter.Add(1); }
    public static void EmailOutboxDeadLetter() { Interlocked.Increment(ref _emailOutboxDeadLetter); EmailOutboxDeadLetterCounter.Add(1); }

    public static SecurityMetricSnapshot Snapshot() => new(
        Interlocked.Read(ref _oauthRejected), Interlocked.Read(ref _twoFactorRejected), Interlocked.Read(ref _refreshReuse),
        Interlocked.Read(ref _webhookRejected), Interlocked.Read(ref _encryptionFailure), Interlocked.Read(ref _reconciliationFailure),
        Interlocked.Read(ref _authorizationDenied), Interlocked.Read(ref _publicTokenRejected), Interlocked.Read(ref _backgroundJobFailure),
        Interlocked.Read(ref _emailOutboxDeadLetter));
}

public sealed record SecurityMetricSnapshot(long OAuthRejected, long TwoFactorRejected, long RefreshReuse, long WebhookRejected, long EncryptionFailure, long ReconciliationFailure, long AuthorizationDenied, long PublicTokenRejected, long BackgroundJobFailure, long EmailOutboxDeadLetter);

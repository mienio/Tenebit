namespace Tenebit.Api.Auth;

/// <summary>
/// Hard lockout for the single admin account, layered on top of the per-IP rate limiter.
///
/// The rate limiter is partitioned by IP, so an attacker spreading attempts across many addresses stays
/// under it indefinitely. This guard counts failures for the account itself, regardless of source, and
/// shuts the door entirely once the threshold is crossed. With a 6-digit TOTP also required, the practical
/// guessing budget becomes negligible.
///
/// In-memory on purpose: Tenebit prod is a single instance, and a lockout that resets on restart is
/// acceptable because restarts are operator-initiated, not attacker-reachable.
/// </summary>
public sealed class AdminLoginGuard
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(30);

    private readonly Lock _gate = new();
    private readonly List<DateTimeOffset> _failures = [];
    private DateTimeOffset? _lockedUntil;

    public bool IsLockedOut(out TimeSpan retryAfter)
    {
        lock (_gate)
        {
            if (_lockedUntil is { } until && until > DateTimeOffset.UtcNow)
            {
                retryAfter = until - DateTimeOffset.UtcNow;
                return true;
            }

            retryAfter = TimeSpan.Zero;
            return false;
        }
    }

    /// <summary>Returns the failure count inside the current window after recording this one.</summary>
    public int RecordFailure()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            _failures.RemoveAll(x => now - x > FailureWindow);
            _failures.Add(now);

            if (_failures.Count >= MaxFailures)
            {
                _lockedUntil = now.Add(LockoutDuration);
                _failures.Clear();
            }

            return _failures.Count;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _failures.Clear();
            _lockedUntil = null;
        }
    }
}

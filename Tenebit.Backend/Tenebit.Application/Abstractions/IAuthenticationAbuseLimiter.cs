namespace Tenebit.Application.Abstractions;

/// <summary>Distributed auth abuse budget keyed by action + normalized account + client address.</summary>
public interface IAuthenticationAbuseLimiter
{
    Task<bool> TryAcquireAsync(string action, string accountKey, string? clientIp, int permitLimit, TimeSpan window, CancellationToken cancellationToken);
}

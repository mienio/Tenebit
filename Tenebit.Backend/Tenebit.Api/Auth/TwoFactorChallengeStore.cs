using Tenebit.Application.Common;
using Tenebit.Api.Auth.OAuth;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Identity;
using Tenebit.Domain.Identity;

namespace Tenebit.Api.Auth;

/// <summary>Shared PostgreSQL-backed one-time second-factor challenge store.</summary>
public sealed class TwoFactorChallengeStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly ITwoFactorChallengeRepository _challenges;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public TwoFactorChallengeStore(ITwoFactorChallengeRepository challenges, IUnitOfWork unitOfWork, IClock clock)
    {
        _challenges = challenges;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<string> CreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var ticket = PkceHelper.NewState();
        _challenges.Add(new TwoFactorChallenge(TokenHasher.Hash(ticket), userId, _clock.UtcNow.Add(Ttl)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<Guid?> ConsumeAsync(string ticket, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return null;
        var challenge = await _challenges.TryConsumeAsync(TokenHasher.Hash(ticket), _clock.UtcNow, cancellationToken);
        if (challenge is null) SecurityTelemetry.TwoFactorRejected();
        return challenge?.OrganizationUserId;
    }
}

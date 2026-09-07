using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Affiliates;

namespace Tenebit.Application.Affiliates;

public sealed record AffiliateClickResult(Guid AttributionToken);

/// <summary>
/// Backs the public <c>GET /r/{code}</c> redirect (spec §6.2). Every click is logged (the raw truth
/// for later audit/dispute), but a burst of clicks from the same hashed IP against the same code in a
/// short window does not inflate <see cref="AffiliateCode.ClickCount"/> - that counter only exists for
/// the affiliate's own dashboard credibility, and bot/refresh-spam clicks have zero effect on
/// commission either way (that only ever comes from a real Paddle transaction).
/// </summary>
public sealed class AffiliateTrackingService
{
    private static readonly TimeSpan ClickDedupeWindow = TimeSpan.FromMinutes(1);
    private const int ClickDedupeThreshold = 5;

    private readonly IAffiliateCodeRepository _codes;
    private readonly IAffiliateClickRepository _clicks;
    private readonly IAffiliateClickHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AffiliateTrackingService(IAffiliateCodeRepository codes, IAffiliateClickRepository clicks, IAffiliateClickHasher hasher, IUnitOfWork unitOfWork, IClock clock)
    {
        _codes = codes;
        _clicks = clicks;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AffiliateClickResult>> RecordClickAsync(string code, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var affiliateCode = await _codes.GetByCodeAsync(code, cancellationToken);
        if (affiliateCode is null || !affiliateCode.IsActive)
        {
            return Result<AffiliateClickResult>.Failure(Error.NotFound("Kod nie istnieje lub jest nieaktywny."));
        }

        var now = _clock.UtcNow;
        var ipHash = _hasher.Hash(string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress);
        var userAgentHash = string.IsNullOrWhiteSpace(userAgent) ? null : _hasher.Hash(userAgent);
        var attributionToken = Guid.NewGuid();

        var recentCount = await _clicks.CountRecentAsync(affiliateCode.Id, ipHash, now.Subtract(ClickDedupeWindow), cancellationToken);
        if (recentCount < ClickDedupeThreshold)
        {
            affiliateCode.RecordClick();
        }

        _clicks.Add(new AffiliateClick(affiliateCode.Id, ipHash, userAgentHash, attributionToken, now));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AffiliateClickResult>.Success(new AffiliateClickResult(attributionToken));
    }
}

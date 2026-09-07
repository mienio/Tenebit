using Tenebit.Application.Abstractions;
using Tenebit.Domain.Affiliates;

namespace Tenebit.Tests.Fakes;

public sealed class InMemoryAffiliateRepository : IAffiliateRepository
{
    public List<Affiliate> Affiliates { get; } = [];

    public Task<Affiliate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Affiliates.FirstOrDefault(x => x.Id == id));

    public Task<Affiliate?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(Affiliates.FirstOrDefault(x => x.Email == normalized));
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult(Affiliates.Any(x => x.Email == normalized));
    }

    public Task<AffiliateSecurityState?> GetSecurityStateAsync(Guid id, CancellationToken cancellationToken)
    {
        var affiliate = Affiliates.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(affiliate is null ? null : new AffiliateSecurityState(affiliate.Status != AffiliateStatus.Blocked, affiliate.SecurityStamp, affiliate.IsEmailVerified));
    }

    public Task<IReadOnlyList<Affiliate>> ListAsync(AffiliateStatus? status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Affiliate>>(Affiliates.Where(x => status == null || x.Status == status).ToList());

    public void Add(Affiliate affiliate) => Affiliates.Add(affiliate);
}

public sealed class InMemoryAffiliateCodeRepository : IAffiliateCodeRepository
{
    public List<AffiliateCode> Codes { get; } = [];

    public Task<AffiliateCode?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return Task.FromResult(Codes.FirstOrDefault(x => x.Code == normalized));
    }

    public Task<AffiliateCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Codes.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<AffiliateCode>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateCode>>(Codes.Where(x => x.AffiliateId == affiliateId).ToList());

    public Task<int> CountActiveByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        Task.FromResult(Codes.Count(x => x.AffiliateId == affiliateId && x.IsActive));

    public void Add(AffiliateCode code) => Codes.Add(code);
}

public sealed class InMemoryAffiliateClickRepository : IAffiliateClickRepository
{
    public List<AffiliateClick> Clicks { get; } = [];

    public Task<int> CountRecentAsync(Guid affiliateCodeId, string ipHash, DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult(Clicks.Count(x => x.AffiliateCodeId == affiliateCodeId && x.IpHash == ipHash && x.ClickedAt >= since));

    public Task<Guid?> FindAffiliateCodeIdByAttributionTokenAsync(Guid attributionToken, CancellationToken cancellationToken) =>
        Task.FromResult(Clicks.Where(x => x.AttributionToken == attributionToken).OrderByDescending(x => x.ClickedAt).Select(x => (Guid?)x.AffiliateCodeId).FirstOrDefault());

    public void Add(AffiliateClick click) => Clicks.Add(click);
}

public sealed class InMemoryAffiliateConversionRepository : IAffiliateConversionRepository
{
    public List<AffiliateConversion> Conversions { get; } = [];

    public Task<bool> ExistsByPaddleTransactionAsync(string paddleTransactionId, CancellationToken cancellationToken) =>
        Task.FromResult(Conversions.Any(x => x.PaddleTransactionId == paddleTransactionId));

    public Task<AffiliateConversion?> GetByPaddleTransactionAsync(string paddleTransactionId, CancellationToken cancellationToken) =>
        Task.FromResult(Conversions.FirstOrDefault(x => x.PaddleTransactionId == paddleTransactionId));

    public Task<DateTimeOffset?> GetFirstSaleDateAsync(Guid affiliateCodeId, Guid organizationId, CancellationToken cancellationToken)
    {
        var first = Conversions
            .Where(x => x.AffiliateCodeId == affiliateCodeId && x.OrganizationId == organizationId && x.EventType == AffiliateConversionEventType.InitialSale)
            .OrderBy(x => x.OccurredAt)
            .Select(x => (DateTimeOffset?)x.OccurredAt)
            .FirstOrDefault();
        return Task.FromResult(first);
    }

    public Task<IReadOnlyList<AffiliateConversion>> ListByAffiliateAsync(Guid affiliateId, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateConversion>>(Conversions
            .Where(x => x.AffiliateId == affiliateId)
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList());

    public Task<int> CountByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        Task.FromResult(Conversions.Count(x => x.AffiliateId == affiliateId));

    public Task<IReadOnlyList<AffiliateConversion>> ListRequiringReviewAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateConversion>>(Conversions.Where(x => x.RequiresReview).ToList());

    public Task<IReadOnlyList<AffiliateConversion>> ListUnassignedInRangeAsync(Guid affiliateId, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateConversion>>(Conversions
            .Where(x => x.AffiliateId == affiliateId && !x.RequiresReview && x.AffiliatePayoutPeriodId == null && x.OccurredAt >= periodStart && x.OccurredAt < periodEnd)
            .ToList());

    public void Add(AffiliateConversion conversion) => Conversions.Add(conversion);
}

public sealed class InMemoryAffiliatePayoutPeriodRepository : IAffiliatePayoutPeriodRepository
{
    public List<AffiliatePayoutPeriod> Periods { get; } = [];

    public Task<AffiliatePayoutPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Periods.FirstOrDefault(x => x.Id == id));

    public Task<AffiliatePayoutPeriod?> GetOpenForMonthAsync(Guid affiliateId, DateTimeOffset periodStart, CancellationToken cancellationToken) =>
        Task.FromResult(Periods.FirstOrDefault(x => x.AffiliateId == affiliateId && x.PeriodStart == periodStart && x.Status == PayoutPeriodStatus.Open));

    public Task<IReadOnlyList<AffiliatePayoutPeriod>> ListOpenEndingBeforeAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliatePayoutPeriod>>(Periods.Where(x => x.Status == PayoutPeriodStatus.Open && x.PeriodEnd <= now).ToList());

    public Task<IReadOnlyList<AffiliatePayoutPeriod>> ListAwaitingPayoutAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliatePayoutPeriod>>(Periods.Where(x => x.AffiliateId == affiliateId && x.Status == PayoutPeriodStatus.AwaitingPayout).OrderBy(x => x.PeriodStart).ToList());

    public Task<IReadOnlyList<AffiliatePayoutPeriod>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliatePayoutPeriod>>(Periods.Where(x => x.AffiliateId == affiliateId).OrderByDescending(x => x.PeriodStart).ToList());

    public void Add(AffiliatePayoutPeriod period) => Periods.Add(period);
}

public sealed class InMemoryAffiliatePayoutRepository : IAffiliatePayoutRepository
{
    public List<AffiliatePayout> Payouts { get; } = [];

    public Task<IReadOnlyList<AffiliatePayout>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliatePayout>>(Payouts.Where(x => x.AffiliateId == affiliateId).OrderByDescending(x => x.MarkedPaidAt).ToList());

    public void Add(AffiliatePayout payout) => Payouts.Add(payout);
}

public sealed class InMemoryAffiliateMessageThreadRepository : IAffiliateMessageThreadRepository
{
    public List<AffiliateMessageThread> Threads { get; } = [];

    public Task<AffiliateMessageThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Threads.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<AffiliateMessageThread>> ListByAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateMessageThread>>(Threads.Where(x => x.AffiliateId == affiliateId).OrderByDescending(x => x.LastMessageAt).ToList());

    public Task<IReadOnlyList<AffiliateMessageThread>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateMessageThread>>(Threads.OrderByDescending(x => x.LastMessageAt).ToList());

    public Task<int> CountUnreadByAdminAsync(CancellationToken cancellationToken) => Task.FromResult(Threads.Count(x => x.UnreadByAdmin));

    public void Add(AffiliateMessageThread thread) => Threads.Add(thread);
}

public sealed class InMemoryAffiliateMessageRepository : IAffiliateMessageRepository
{
    public List<AffiliateMessage> Messages { get; } = [];

    public Task<IReadOnlyList<AffiliateMessage>> ListByThreadAsync(Guid threadId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateMessage>>(Messages.Where(x => x.ThreadId == threadId).OrderBy(x => x.SentAt).ToList());

    public void Add(AffiliateMessage message) => Messages.Add(message);
}

public sealed class InMemoryAffiliateProgramSettingsRepository : IAffiliateProgramSettingsRepository
{
    public AffiliateProgramSettings Settings { get; set; } = AffiliateProgramSettings.CreateDefault();

    public Task<AffiliateProgramSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);
}

public sealed class InMemoryAffiliateCountryDiscountRuleRepository : IAffiliateCountryDiscountRuleRepository
{
    public List<AffiliateCountryDiscountRule> Rules { get; } = [];

    public Task<IReadOnlyList<AffiliateCountryDiscountRule>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AffiliateCountryDiscountRule>>(Rules.OrderBy(x => x.CountryCode).ToList());

    public Task<AffiliateCountryDiscountRule?> GetByCountryAsync(string countryCode, CancellationToken cancellationToken)
    {
        var normalized = countryCode.Trim().ToUpperInvariant();
        return Task.FromResult(Rules.FirstOrDefault(x => x.CountryCode == normalized));
    }

    public void Add(AffiliateCountryDiscountRule rule) => Rules.Add(rule);
    public void Remove(AffiliateCountryDiscountRule rule) => Rules.Remove(rule);
}

public sealed class InMemoryAffiliateRefreshTokenRepository : IAffiliateRefreshTokenRepository
{
    public List<AffiliateRefreshToken> Tokens { get; } = [];

    public Task<AffiliateRefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash));

    public Task<AffiliateRefreshToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.RevokedAt == null && x.ExpiresAt > now));

    public Task<bool> TryMarkRotatedAsync(Guid tokenId, Guid replacementTokenId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = Tokens.FirstOrDefault(x => x.Id == tokenId && x.RevokedAt == null && x.ExpiresAt > now);
        if (token is null) return Task.FromResult(false);
        token.MarkRotated(replacementTokenId, now);
        return Task.FromResult(true);
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(x => x.FamilyId == familyId && x.RevokedAt == null)) token.Revoke(now, reason);
        return Task.CompletedTask;
    }

    public Task RevokeAllForAffiliateAsync(Guid affiliateId, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(x => x.AffiliateId == affiliateId && x.RevokedAt == null)) token.Revoke(reason: "security_state_changed");
        return Task.CompletedTask;
    }

    public void Add(AffiliateRefreshToken token) => Tokens.Add(token);
}

public sealed class InMemoryAffiliatePasswordResetTokenRepository : IAffiliatePasswordResetTokenRepository
{
    public List<AffiliatePasswordResetToken> Tokens { get; } = [];

    public Task<AffiliatePasswordResetToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now));

    public Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now);
        if (token is null) return Task.FromResult((Guid?)null);
        token.MarkUsed();
        return Task.FromResult<Guid?>(token.AffiliateId);
    }

    public Task RevokeUnusedForAffiliateAsync(Guid affiliateId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(x => x.AffiliateId == affiliateId && x.UsedAt == null)) token.MarkUsed();
        return Task.CompletedTask;
    }

    public void Add(AffiliatePasswordResetToken token) => Tokens.Add(token);
}

public sealed class InMemoryAffiliateEmailVerificationTokenRepository : IAffiliateEmailVerificationTokenRepository
{
    public List<AffiliateEmailVerificationToken> Tokens { get; } = [];

    public Task<AffiliateEmailVerificationToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now));

    public Task<Guid?> TryConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = Tokens.FirstOrDefault(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now);
        if (token is null) return Task.FromResult((Guid?)null);
        token.MarkUsed();
        return Task.FromResult<Guid?>(token.AffiliateId);
    }

    public Task RevokeUnusedForAffiliateAsync(Guid affiliateId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(x => x.AffiliateId == affiliateId && x.UsedAt == null)) token.MarkUsed();
        return Task.CompletedTask;
    }

    public void Add(AffiliateEmailVerificationToken token) => Tokens.Add(token);
}

public sealed class InMemoryAffiliateSecurityStateCache : IAffiliateSecurityStateCache
{
    private readonly Dictionary<Guid, AffiliateSecurityState> _entries = [];

    public bool TryGet(Guid affiliateId, out AffiliateSecurityState state) => _entries.TryGetValue(affiliateId, out state!);
    public void Set(Guid affiliateId, AffiliateSecurityState state, TimeSpan ttl) => _entries[affiliateId] = state;
    public void Remove(Guid affiliateId) => _entries.Remove(affiliateId);
}

public sealed class InMemoryAffiliateClickHasher : IAffiliateClickHasher
{
    public string Hash(string value) => $"hash:{value}";
}

/// <summary>Minimal fake covering only what AffiliateAdminService/AffiliateProgramSettingsAdminService
/// actually call (AddAdminAudit) - every other member of this large cross-tenant interface is
/// irrelevant to those two services and stubbed out.</summary>
public sealed class InMemoryAdminRepository : IAdminRepository
{
    public List<Tenebit.Domain.Identity.AdminAuditLog> AuditEntries { get; } = [];

    public void AddAdminAudit(Tenebit.Domain.Identity.AdminAuditLog entry) => AuditEntries.Add(entry);

    public Task<PlatformTotals> GetTotalsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new PlatformTotals(0, 0, 0, 0, 0, 0, 0, 0));

    public Task<IReadOnlyList<DailyCount>> GetAssetsCreatedPerDayAsync(DateOnly from, DateOnly to, Guid? organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DailyCount>>([]);

    public Task<IReadOnlyList<DailyCount>> GetOrganizationsCreatedPerDayAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DailyCount>>([]);

    public Task<IReadOnlyList<DailyCount>> GetLoginsPerDayAsync(DateOnly from, DateOnly to, bool succeeded, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DailyCount>>([]);

    public Task<int> CountLoginsAsync(DateOnly from, DateOnly to, bool succeeded, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<IReadOnlyList<(string Label, int Count)>> GetAssetStatusBreakdownAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(string Label, int Count)>>([]);

    public Task<IReadOnlyList<(string Label, int Count)>> GetAssetCategoryBreakdownAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(string Label, int Count)>>([]);

    public Task<IReadOnlyList<(string Label, int Count)>> GetPeopleStatusBreakdownAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(string Label, int Count)>>([]);

    public Task<IReadOnlyList<(string PlanKey, int Count)>> GetPlanDistributionAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<(string PlanKey, int Count)>>([]);

    public Task<(IReadOnlyList<AdminUserEntry> Items, int Total)> ListUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<AdminUserEntry> Items, int Total)>(([], 0));

    public Task<AdminUserEntry?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<AdminUserEntry?>(null);

    public Task<(IReadOnlyList<LoginEventEntry> Items, int Total)> ListLoginEventsAsync(string? search, bool? succeededOnly, Guid? organizationId, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<LoginEventEntry> Items, int Total)>(([], 0));

    public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetReviewedOrganizationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, DateTimeOffset>>(new Dictionary<Guid, DateTimeOffset>());

    public Task<IReadOnlyList<Tenebit.Domain.Identity.AdminAuditLog>> ListAdminAuditAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Tenebit.Domain.Identity.AdminAuditLog>>(AuditEntries);

    public void AddLoginEvent(Tenebit.Domain.Identity.LoginEvent loginEvent) { }

    public Task<int> CountRecentModerationActionsAsync(DateTimeOffset since, CancellationToken cancellationToken) => Task.FromResult(0);
}

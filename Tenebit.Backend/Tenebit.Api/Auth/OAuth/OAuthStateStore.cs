using Tenebit.Application.Common;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Identity;
using Tenebit.Domain.Identity;

namespace Tenebit.Api.Auth.OAuth;

public sealed record OAuthStateEntry(string Provider, string CodeVerifier, string ReturnPath, string CorrelationHash, string Nonce);

/// <summary>PostgreSQL-backed OAuth transaction store. State is shared across API replicas and consumed atomically.</summary>
public sealed class OAuthStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly IOAuthTransactionRepository _transactions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public OAuthStateStore(IOAuthTransactionRepository transactions, IUnitOfWork unitOfWork, IClock clock)
    {
        _transactions = transactions;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<string> CreateAsync(string provider, string codeVerifier, string returnPath, string correlationHash, string nonce, CancellationToken cancellationToken)
    {
        var state = PkceHelper.NewState();
        _transactions.Add(new OAuthTransaction(TokenHasher.Hash(state), provider, codeVerifier, returnPath, correlationHash, nonce, _clock.UtcNow.Add(Ttl)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return state;
    }

    public async Task<OAuthStateEntry?> TryConsumeAsync(string state, string provider, string? correlationRaw, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(correlationRaw))
        {
            SecurityTelemetry.OAuthRejected();
            return null;
        }
        var entry = await _transactions.TryConsumeAsync(TokenHasher.Hash(state), provider, TokenHasher.Hash(correlationRaw), _clock.UtcNow, cancellationToken);
        if (entry is null)
        {
            SecurityTelemetry.OAuthRejected();
            return null;
        }
        return new OAuthStateEntry(entry.Provider, entry.CodeVerifier, entry.ReturnPath, entry.CorrelationHash, entry.Nonce);
    }
}

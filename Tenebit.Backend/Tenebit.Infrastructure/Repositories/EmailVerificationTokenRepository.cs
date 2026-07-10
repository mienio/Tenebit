using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly TenebitDbContext _db;
    public EmailVerificationTokenRepository(TenebitDbContext db) => _db = db;

    public Task<EmailVerificationToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.EmailVerificationTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, cancellationToken);

    public void Add(EmailVerificationToken token) => _db.EmailVerificationTokens.Add(token);
}

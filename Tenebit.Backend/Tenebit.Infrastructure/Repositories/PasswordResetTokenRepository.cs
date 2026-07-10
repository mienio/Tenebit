using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Identity;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly TenebitDbContext _db;
    public PasswordResetTokenRepository(TenebitDbContext db) => _db = db;

    public Task<PasswordResetToken?> FindValidAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.PasswordResetTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, cancellationToken);

    public void Add(PasswordResetToken token) => _db.PasswordResetTokens.Add(token);
}

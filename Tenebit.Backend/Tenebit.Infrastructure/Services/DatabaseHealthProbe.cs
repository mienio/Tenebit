using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

public sealed class DatabaseHealthProbe : IDatabaseHealthProbe
{
    private readonly TenebitDbContext _db;
    public DatabaseHealthProbe(TenebitDbContext db) => _db = db;

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        _db.Database.CanConnectAsync(cancellationToken);
}

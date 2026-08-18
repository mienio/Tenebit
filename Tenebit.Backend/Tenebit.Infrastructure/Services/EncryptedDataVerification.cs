using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

public static class EncryptedDataVerification
{
    /// <summary>
    /// Forces materialization of every application-encrypted field. EF value converters call the active
    /// FieldEncryptor during materialization, so an unavailable historical key or corrupted ciphertext
    /// fails the drill without ever printing plaintext.
    /// </summary>
    public static async Task VerifyEncryptedDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();

        _ = await db.OrganizationUsers.AsNoTracking()
            .Where(x => x.TotpSecret != null)
            .Select(x => new { x.Id, x.TotpSecret })
            .ToListAsync(cancellationToken);

        _ = await db.Licenses.AsNoTracking()
            .Where(x => x.LicenseKey != null)
            .Select(x => new { x.Id, x.LicenseKey })
            .ToListAsync(cancellationToken);

        _ = await db.Assets.AsNoTracking()
            .Include(x => x.FieldValues)
            .ToListAsync(cancellationToken);
    }
}

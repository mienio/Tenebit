using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Tenebit.Application.Assets;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Seed;

public sealed class DefaultDataSeeder
{
    private readonly TenebitDbContext _db;
    private readonly IConfiguration _configuration;

    public DefaultDataSeeder(TenebitDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var organizationId = Guid.Parse(_configuration["Auth:DevelopmentOrganizationId"] ?? "11111111-1111-1111-1111-111111111111");
        var organization = await _db.Organizations.FirstOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            organization = Organization.CreateSeed(organizationId, "Tenebit Demo", "PL", "pl", "PLN", "Europe/Warsaw");
            _db.Organizations.Add(organization);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await SeedStarterCategoriesForAllOrganizationsAsync(cancellationToken);

        // BUG FIX: The second check below previously used GetValue("Seed:DemoData", true) — a different
        // default than the guard above (false). Since we already checked the flag above, the second
        // check was both redundant and misleading for readers. Removed entirely.
        if (!_configuration.GetValue("Seed:DemoData", false))
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var laptopCategory = await _db.AssetCategories.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.Name == "Laptopy", cancellationToken);

        // Seed demo people, assets and procedures only if no people exist yet for this org.
        if (!await _db.People.AnyAsync(x => x.OrganizationId == organizationId, cancellationToken))
        {
            var team = new Team(organizationId, "Engineering", null, "ENG");
            var person = new Person(organizationId, "Anna", "Nowak", "anna.nowak@example.com");
            person.Update("Anna", "Nowak", "anna.nowak@example.com", "+48 500 100 200", "EMP-001", PersonRelationType.Employee, "Programistka", team.Id, null, "Warszawa", "ENG");
            var category = laptopCategory ?? await _db.AssetCategories.FirstAsync(x => x.OrganizationId == organizationId && x.Name == "Laptopy", cancellationToken);
            var asset = new Asset(organizationId, category.Id, "Dell Latitude 7440", "TB-LAP-0001");
            asset.UpdateCore("Dell Latitude 7440", "TB-LAP-0001", "DL7440-DEMO", category.Id, "Warszawa / Magazyn", "Dell", "Latitude 7440", 6200m, "PLN", DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(30)), team.Id);
            var procedure = new Procedure(organizationId, "Regulamin korzystania ze sprzetu firmowego", "1.0", "HR / IT", true);
            procedure.Update("Regulamin korzystania ze sprzetu firmowego", "1.0", "HR / IT", "Wszyscy pracownicy", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), true);
            _db.Teams.Add(team);
            _db.People.Add(person);
            _db.Assets.Add(asset);
            _db.Procedures.Add(procedure);
            _db.ActivityLogs.Add(new ActivityLog(organizationId, "seed.demo.created", "organization", organizationId, "system", "Dane demonstracyjne utworzone przy pierwszym uruchomieniu.", DateTimeOffset.UtcNow));
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // Every organization gets its own private copy of the starter category pack — never a shared/global
    // row — so per-tenant isolation stays intact through the existing OrganizationId filtering. Tops up
    // by name so organizations that already created a few categories of their own still receive whichever
    // starter categories they're missing, without touching or duplicating what they already have.
    private async Task SeedStarterCategoriesForAllOrganizationsAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await _db.Organizations.Select(x => x.Id).ToListAsync(cancellationToken);
        if (organizationIds.Count == 0) return;

        var existingNamesByOrganization = (await _db.AssetCategories
                .Select(x => new { x.OrganizationId, x.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));

        foreach (var organizationId in organizationIds)
        {
            var existingNames = existingNamesByOrganization.GetValueOrDefault(organizationId) ?? [];
            var missing = StarterAssetCategories.Create(organizationId).Where(x => !existingNames.Contains(x.Name));
            _db.AssetCategories.AddRange(missing);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

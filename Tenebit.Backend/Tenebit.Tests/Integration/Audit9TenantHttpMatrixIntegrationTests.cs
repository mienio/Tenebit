using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Procedures;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Tests.Integration;

/// <summary>
/// Negative HTTP matrix for the tenant boundary required by audit9. Data is deliberately inserted for tenant B
/// through the real PostgreSQL DbContext and then requested with tenant A's real JWT, so these assertions catch
/// missing repository organization filters as well as endpoint/application authorization regressions.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class Audit9TenantHttpMatrixIntegrationTests : IClassFixture<TenebitApiFactory>
{
    private readonly TenebitApiFactory _factory;

    public Audit9TenantHttpMatrixIntegrationTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TenantA_CannotReadCriticalResourcesOwnedByTenantB()
    {
        var (organizationA, _, tokenA) = await _factory.SeedTenantAsync("TenantMatrixA", "owner");
        var (organizationB, _, personB, _) = await _factory.SeedTenantWithPersonAsync("TenantMatrixB", "owner");

        AssetCategory categoryA;
        Asset assetB;
        Procedure procedureB;
        Assignment assignmentB;
        ServiceTicket ticketB;
        AssetEvidence evidenceB;
        OffboardingCase offboardingB;
        AssetAuditCampaign auditB;
        Location locationB;
        License licenseB;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            categoryA = new AssetCategory(organizationA.Id, $"CategoryA-{Guid.NewGuid():N}", AssetCategoryType.Physical, null);
            var categoryB = new AssetCategory(organizationB.Id, $"Category-{Guid.NewGuid():N}", AssetCategoryType.Physical, null);
            assetB = new Asset(organizationB.Id, categoryB.Id, "Tenant B asset", $"B-{Guid.NewGuid():N}"[..14]);
            procedureB = new Procedure(organizationB.Id, "Tenant B procedure", "1.0", "Operations", false);
            assignmentB = new Assignment(organizationB.Id, personB.Id, $"B-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, null, null, "matrix");
            ticketB = new ServiceTicket(organizationB.Id, assetB.Id, "Tenant B vendor", "private ticket", null);
            evidenceB = new AssetEvidence(
                organizationB.Id,
                assetB.Id,
                null,
                EvidencePhase.Issue,
                "evidence.png",
                "image/png",
                [1, 2, 3],
                new string('a', 64),
                "tenant B evidence",
                "matrix",
                EvidenceUploadSource.AuthenticatedUser,
                DateTimeOffset.UtcNow);
            offboardingB = new OffboardingCase(
                organizationB.Id,
                personB.Id,
                DateTimeOffset.UtcNow.AddDays(7),
                DateTimeOffset.UtcNow.AddDays(10),
                null,
                "tenant B offboarding",
                null,
                true,
                true,
                true,
                "matrix",
                DateTimeOffset.UtcNow);
            auditB = new AssetAuditCampaign(
                organizationB.Id,
                "Tenant B audit",
                "private audit",
                DateTimeOffset.UtcNow.AddDays(14),
                null,
                "matrix",
                DateTimeOffset.UtcNow);
            locationB = new Location(organizationB.Id, $"Private-{Guid.NewGuid():N}", "Room", null);
            licenseB = new License(organizationB.Id, $"PrivateLicense-{Guid.NewGuid():N}", "Vendor B", null, 10, null, null);

            db.AssetCategories.AddRange(categoryA, categoryB);
            db.Assets.Add(assetB);
            db.Procedures.Add(procedureB);
            db.Assignments.Add(assignmentB);
            db.ServiceTickets.Add(ticketB);
            db.AssetEvidence.Add(evidenceB);
            db.OffboardingCases.Add(offboardingB);
            db.AssetAuditCampaigns.Add(auditB);
            db.Locations.Add(locationB);
            db.Licenses.Add(licenseB);
            await db.SaveChangesAsync();
        }

        var clientA = _factory.CreateAuthenticatedClient(tokenA);
        var checks = new[]
        {
            $"/api/people/{personB.Id}",
            $"/api/assets/{assetB.Id}",
            $"/api/procedures/{procedureB.Id}",
            $"/api/assignments/{assignmentB.Id}",
            $"/api/service-tickets/{ticketB.Id}",
            $"/api/evidence/{evidenceB.Id}",
            $"/api/offboarding/{offboardingB.Id}",
            $"/api/asset-audits/{auditB.Id}",
            $"/api/locations/{locationB.Id}/inventory"
        };

        foreach (var path in checks)
        {
            var response = await clientA.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // Tenant-wide list/filter endpoints must not enumerate tenant B identifiers either.
        var listChecks = new (string Path, string ForbiddenId)[]
        {
            ("/api/people", personB.Id.ToString()),
            ("/api/assets", assetB.Id.ToString()),
            ("/api/procedures", procedureB.Id.ToString()),
            ("/api/assignments", assignmentB.Id.ToString()),
            ("/api/service-tickets", ticketB.Id.ToString()),
            ("/api/offboarding", offboardingB.Id.ToString()),
            ("/api/asset-audits", auditB.Id.ToString()),
            ("/api/locations", locationB.Id.ToString()),
            ("/api/licenses", licenseB.Id.ToString())
        };
        foreach (var (path, forbiddenId) in listChecks)
        {
            var json = await clientA.GetStringAsync(path);
            Assert.DoesNotContain(forbiddenId, json, StringComparison.OrdinalIgnoreCase);
        }

        // Asset child/subresource routes are part of the same tenant boundary.
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/assets/{assetB.Id}/evidence")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/assets/{assetB.Id}/service-tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/assets/{assetB.Id}/inspection")).StatusCode);

        // Mutating endpoints receive valid-shaped payloads. The foreign resource ID itself must be the reason
        // for rejection, and the API must not disclose that the row exists in tenant B.
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/people/{personB.Id}", new
        {
            FirstName = "Blocked", LastName = "Mutation", Email = $"blocked-{Guid.NewGuid():N}@example.test",
            Phone = (string?)null, EmployeeNumber = (string?)null, RelationType = personB.RelationType, JobTitle = (string?)null,
            TeamId = (Guid?)null, ManagerId = (Guid?)null, Location = (string?)null, CostCenter = (string?)null, IsActive = true, PreferredLanguage = "en"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.DeleteAsync($"/api/people/{personB.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsJsonAsync($"/api/people/{personB.Id}/offboarding", new
        {
            EmploymentEndsAt = DateTimeOffset.UtcNow.AddDays(10)
        })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/assets/{assetB.Id}", new
        {
            Name = "Blocked asset", AssetTag = $"A-{Guid.NewGuid():N}"[..14], SerialNumber = (string?)null, CategoryId = categoryA.Id,
            Status = 0, Location = (string?)null, Manufacturer = (string?)null, Model = (string?)null, PurchasePrice = (decimal?)null,
            Currency = (string?)null, PurchaseDate = (DateOnly?)null, WarrantyUntil = (DateOnly?)null, TeamId = (Guid?)null, CustomFields = (object?)null
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.DeleteAsync($"/api/assets/{assetB.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/procedures/{procedureB.Id}", new
        {
            Title = "Blocked procedure", Version = "1.1", Owner = "Operations", AppliesTo = (string?)null, ReviewDate = (DateOnly?)null, RequiresAcceptance = false
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsync($"/api/procedures/{procedureB.Id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsync($"/api/procedures/{procedureB.Id}/archive", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsync($"/api/assignments/{assignmentB.Id}/acceptance-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsJsonAsync($"/api/assignments/{assignmentB.Id}/return", new
        {
            ReturnCondition = "Good", DestinationLocation = "HQ", Assets = (object?)null
        })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/service-tickets/{ticketB.Id}", new
        {
            Vendor = "Blocked vendor", Description = "blocked", EstimatedCost = (decimal?)null, Currency = (string?)null, SlaDueAt = (DateTimeOffset?)null
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsJsonAsync($"/api/service-tickets/{ticketB.Id}/cancel", new
        {
            Resolution = "blocked"
        })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/evidence/{evidenceB.Id}/legal-hold", new { Enabled = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.DeleteAsync($"/api/evidence/{evidenceB.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/offboarding/{offboardingB.Id}", new
        {
            EmploymentEndsAt = DateTimeOffset.UtcNow.AddDays(7), ReturnDueDate = DateTimeOffset.UtcNow.AddDays(10),
            DefaultReturnLocation = (string?)null, Notes = "blocked", ProcessOwnerId = (Guid?)null,
            BlockNewReservations = true, CancelFutureReservations = true, AutoReleaseLicenses = true
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsJsonAsync($"/api/offboarding/{offboardingB.Id}/cancel", new { Reason = "blocked" })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/asset-audits/{auditB.Id}", new
        {
            Name = "Blocked audit", Description = (string?)null, DueDate = DateTimeOffset.UtcNow.AddDays(14),
            Scope = new { Type = 0, TeamIds = (Guid[]?)null, Locations = (string[]?)null, AssetCategoryIds = (Guid[]?)null, PersonIds = (Guid[]?)null }
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PostAsync($"/api/asset-audits/{auditB.Id}/cancel", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/locations/{locationB.Id}", new
        {
            Name = "Blocked room", Type = "Room", ParentId = (Guid?)null, IsActive = true
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.DeleteAsync($"/api/locations/{locationB.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/licenses/{licenseB.Id}", new
        {
            Name = "Blocked license", Vendor = "Vendor", LicenseKey = (string?)null, SeatsTotal = 10, ExpiresAt = (DateOnly?)null, Notes = (string?)null
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.DeleteAsync($"/api/licenses/{licenseB.Id}")).StatusCode);

        // Finally, prove tenant B rows still exist after all rejected operations.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            Assert.True(await db.People.AnyAsync(x => x.Id == personB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.Assets.AnyAsync(x => x.Id == assetB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.Procedures.AnyAsync(x => x.Id == procedureB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.Assignments.AnyAsync(x => x.Id == assignmentB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.ServiceTickets.AnyAsync(x => x.Id == ticketB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.AssetEvidence.AnyAsync(x => x.Id == evidenceB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.OffboardingCases.AnyAsync(x => x.Id == offboardingB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.AssetAuditCampaigns.AnyAsync(x => x.Id == auditB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.Locations.AnyAsync(x => x.Id == locationB.Id && x.OrganizationId == organizationB.Id));
            Assert.True(await db.Licenses.AnyAsync(x => x.Id == licenseB.Id && x.OrganizationId == organizationB.Id));
        }
    }
}

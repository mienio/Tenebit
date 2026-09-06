using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Domain.Assets;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Tests.Integration;

/// <summary>
/// Regression for a production incident: /api/public/scan/{scanCode} and its /report sibling were added
/// without .AllowAnonymous(), so they inherited the api group's default RequireAuthorization() and every
/// real-world scan - which by definition comes from someone who is not logged in - returned 401 instead
/// of the public asset lookup. A logged-in test client would never have caught this.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class PublicAssetScanAnonymousAccessTests : IClassFixture<TenebitApiFactory>
{
    private readonly TenebitApiFactory _factory;

    public PublicAssetScanAnonymousAccessTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AnonymousClient_CanResolveAssetByScanCode()
    {
        var (organization, _, _) = await _factory.SeedTenantAsync("ScanMatrixA", "owner");
        Asset asset;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            var category = new AssetCategory(organization.Id, $"Category-{Guid.NewGuid():N}", AssetCategoryType.Physical, null);
            asset = new Asset(organization.Id, category.Id, "Scanned laptop", $"A-{Guid.NewGuid():N}"[..14]);
            db.AssetCategories.Add(category);
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync($"/api/public/scan/{asset.ScanCode}");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousClient_CanReportIssueByScanCode()
    {
        var (organization, _, _) = await _factory.SeedTenantAsync("ScanMatrixB", "owner");
        Asset asset;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
            var category = new AssetCategory(organization.Id, $"Category-{Guid.NewGuid():N}", AssetCategoryType.Physical, null);
            asset = new Asset(organization.Id, category.Id, "Scanned laptop", $"A-{Guid.NewGuid():N}"[..14]);
            db.AssetCategories.Add(category);
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        }

        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.PostAsJsonAsync($"/api/public/scan/{asset.ScanCode}/report", new { Message = "Screen is cracked" });

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

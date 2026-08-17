using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tenebit.Application.Assets;
using Tenebit.Domain.Assets;

namespace Tenebit.Tests.Integration;

/// <summary>Audyt AUD-040: "red team tenant isolation" na prawdziwym API + PostgreSQL. Każdy test zakłada
/// dwie niezależne organizacje (A, B) i sprawdza, że A nie może odczytać/zmodyfikować danych B — oraz że
/// role bez uprawnień dostają 403 z prawdziwego middleware auth/authz, nie tylko z Application-layer mocka.</summary>
public sealed class TenantIsolationTests : IClassFixture<TenebitApiFactory>
{
    // Serwer serializuje enumy jako stringi (Program.cs: JsonStringEnumConverter na JsonOptions Minimal API),
    // ale HttpClient.ReadFromJsonAsync<T>() bez jawnych opcji używa domyślnego JsonSerializerOptions, który
    // tego nie wie — bez tego AssetCategoryResponse.Type/AssetResponse.Status rzucają JsonException.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TenebitApiFactory _factory;

    public TenantIsolationTests(TenebitApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid CategoryId)> SeedOrgWithCategoryAsync(string prefix)
    {
        var (_, _, token) = await _factory.SeedTenantAsync(prefix, "owner");
        var client = _factory.CreateAuthenticatedClient(token);
        var categoryResponse = await client.PostAsJsonAsync("/api/asset-categories", new CreateAssetCategoryRequest($"Kategoria {prefix}", AssetCategoryType.Physical, null, null));
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<AssetCategoryResponse>(JsonOptions);
        return (client, category!.Id);
    }

    private static async Task<AssetResponse> CreateAssetAsync(HttpClient client, Guid categoryId, string tag)
    {
        var response = await client.PostAsJsonAsync("/api/assets", new CreateAssetRequest($"Laptop {tag}", tag, null, categoryId, null, null, null, null, null, null, null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task OrgB_cannot_read_asset_created_by_org_A()
    {
        var (clientA, categoryA) = await SeedOrgWithCategoryAsync("A");
        var (clientB, _) = await SeedOrgWithCategoryAsync("B");
        var asset = await CreateAssetAsync(clientA, categoryA, $"TAG-{Guid.NewGuid():N}"[..12]);

        var response = await clientB.GetAsync($"/api/assets/{asset.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrgB_cannot_update_asset_created_by_org_A()
    {
        var (clientA, categoryA) = await SeedOrgWithCategoryAsync("A");
        var (clientB, categoryB) = await SeedOrgWithCategoryAsync("B");
        var asset = await CreateAssetAsync(clientA, categoryA, $"TAG-{Guid.NewGuid():N}"[..12]);

        var updateResponse = await clientB.PutAsJsonAsync($"/api/assets/{asset.Id}",
            new UpdateAssetRequest("Przejęty laptop", asset.AssetTag, null, categoryB, asset.Status, null, null, null, null, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var stillOwnedByA = await clientA.GetFromJsonAsync<AssetResponse>($"/api/assets/{asset.Id}", JsonOptions);
        Assert.Equal("Laptop " + asset.AssetTag, stillOwnedByA!.Name);
    }

    [Fact]
    public async Task OrgB_cannot_delete_asset_created_by_org_A()
    {
        var (clientA, categoryA) = await SeedOrgWithCategoryAsync("A");
        var (clientB, _) = await SeedOrgWithCategoryAsync("B");
        var asset = await CreateAssetAsync(clientA, categoryA, $"TAG-{Guid.NewGuid():N}"[..12]);

        var deleteResponse = await clientB.DeleteAsync($"/api/assets/{asset.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        var stillThere = await clientA.GetAsync($"/api/assets/{asset.Id}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task OrgA_asset_list_never_contains_orgB_assets()
    {
        var (clientA, categoryA) = await SeedOrgWithCategoryAsync("A");
        var (clientB, categoryB) = await SeedOrgWithCategoryAsync("B");
        var tagB = $"TAG-{Guid.NewGuid():N}"[..12];
        await CreateAssetAsync(clientA, categoryA, $"TAG-{Guid.NewGuid():N}"[..12]);
        await CreateAssetAsync(clientB, categoryB, tagB);

        var listA = await clientA.GetFromJsonAsync<List<AssetResponse>>("/api/assets", JsonOptions);

        Assert.DoesNotContain(listA!, x => x.AssetTag == tagB);
    }

    [Fact]
    public async Task Employee_cannot_create_location()
    {
        var (_, _, employeeToken) = await _factory.SeedTenantAsync("Emp", "employee");
        var client = _factory.CreateAuthenticatedClient(employeeToken);

        var response = await client.PostAsJsonAsync("/api/locations", new { Name = "Magazyn", Type = "Room", ParentId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_location()
    {
        var (_, _, ownerToken) = await _factory.SeedTenantAsync("Own", "owner");
        var client = _factory.CreateAuthenticatedClient(ownerToken);

        var response = await client.PostAsJsonAsync("/api/locations", new { Name = "Magazyn główny", Type = "Room", ParentId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_list_organization_users()
    {
        var (_, _, employeeToken) = await _factory.SeedTenantAsync("Emp", "employee");
        var client = _factory.CreateAuthenticatedClient(employeeToken);

        var response = await client.GetAsync("/api/organization-users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/assets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_assignment_endpoint_rejects_unknown_token()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/public/assignments/{Guid.NewGuid():N}not-a-real-token");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

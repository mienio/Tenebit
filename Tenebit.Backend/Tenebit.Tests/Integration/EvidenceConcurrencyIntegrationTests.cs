using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenebit.Application.Assets;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Tests.Integration;

public sealed class EvidenceConcurrencyIntegrationTests : IClassFixture<TenebitApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Valid 1x1 PNG; ImageSharp must fully identify/decode it, so this test exercises the production sanitizer.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC");

    private readonly TenebitApiFactory _factory;

    public EvidenceConcurrencyIntegrationTests(TenebitApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Six_concurrent_uploads_persist_exactly_five()
    {
        var (organization, _, token) = await _factory.SeedTenantAsync("EvidenceRace", "owner");
        var client = _factory.CreateAuthenticatedClient(token);

        var categoryResponse = await client.PostAsJsonAsync("/api/asset-categories",
            new CreateAssetCategoryRequest("Evidence category", AssetCategoryType.Physical, null, null));
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<AssetCategoryResponse>(JsonOptions);

        var assetResponse = await client.PostAsJsonAsync("/api/assets",
            new CreateAssetRequest("Evidence asset", $"EV-{Guid.NewGuid():N}"[..18], null, category!.Id, null, null, null, null, null, null, null, null, null));
        assetResponse.EnsureSuccessStatusCode();
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetResponse>(JsonOptions);

        var uploads = Enumerable.Range(1, 6).Select(index => UploadAsync(client, asset!.Id, index)).ToArray();
        var responses = await Task.WhenAll(uploads);

        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        var persisted = await db.AssetEvidence.CountAsync(x =>
            x.OrganizationId == organization.Id && x.AssetId == asset.Id && x.Phase == EvidencePhase.Issue);
        Assert.Equal(5, persisted);
    }

    [Fact]
    public async Task Oversized_json_is_rejected_before_endpoint_model_binding()
    {
        var client = _factory.CreateClient();
        var payload = "{\"email\":\"user@example.com\",\"password\":\"" + new string('x', 1024 * 1024) + "\"}";
        using var content = new StringContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync("/api/auth/login", content);

        Assert.Equal((HttpStatusCode)413, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid assetId, int index)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(nameof(EvidencePhase.Issue)), "phase");
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", $"photo-{index}.png");
        return await client.PostAsync($"/api/assets/{assetId}/evidence", form);
    }
}

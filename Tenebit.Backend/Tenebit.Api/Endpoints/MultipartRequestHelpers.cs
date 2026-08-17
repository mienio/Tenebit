using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Tenebit.Api.Auth;
using Tenebit.Api.Http;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Alerts;
using Tenebit.Application.Assets;
using Tenebit.Application.Assignments;
using Tenebit.Application.Audit;
using Tenebit.Domain.Alerts;
using Tenebit.Application.Audits;
using Tenebit.Domain.Audits;
using Tenebit.Application.Dashboard;
using Tenebit.Application.Evidence;
using Tenebit.Application.Identity;
using Tenebit.Application.JobProfiles;
using Tenebit.Application.Licenses;
using Tenebit.Application.Offboarding;
using Tenebit.Application.Onboarding;
using Tenebit.Application.Organizations;
using Tenebit.Application.People;
using Tenebit.Application.Procedures;
using Tenebit.Application.Reservations;
using Tenebit.Application.Settings;
using Tenebit.Application.Subscriptions;
using Tenebit.Application.Workspace;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Reservations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Api.Endpoints;

internal static class MultipartRequestHelpers
{
    internal static readonly JsonSerializerOptions MultipartJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    internal static T? DeserializePart<T>(IFormCollection form, string name)
    {
        var json = form[name].ToString();
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, MultipartJsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    internal static Dictionary<string, EvidenceManifestEntry>? DeserializeManifest(IFormCollection form)
    {
        var json = form["evidenceManifest"].ToString();
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, EvidenceManifestEntry>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, EvidenceManifestEntry>>(json, MultipartJsonOptions) ?? new Dictionary<string, EvidenceManifestEntry>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static async Task<byte[]> ReadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    // Limity ustawione tuż przed odczytem body (audyt P0.4) — Kestrel przerywa strumień, gdy przekroczy
    // limit, zamiast pozwolić handlerowi zbuforować cały upload do pamięci przed walidacją rozmiaru na
    // poziomie serwisu (AssetEvidenceService: 5 MB/plik, ProcedureDocument: 25 MB). Marginesy uwzględniają
    // narzut multipart oraz — dla wsadów wieloplikowych — kilka zdjęć naraz.
    internal const long MaxSingleEvidenceUploadBytes = 8L * 1024 * 1024;
    internal const long MaxProcedureDocumentUploadBytes = 27L * 1024 * 1024;
    internal const long MaxEvidenceBundleUploadBytes = 40L * 1024 * 1024;

    internal static void LimitRequestBody(HttpRequest request, long maxBytes)
    {
        var feature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is not null && !feature.IsReadOnly)
        {
            feature.MaxRequestBodySize = maxBytes;
        }
    }
}

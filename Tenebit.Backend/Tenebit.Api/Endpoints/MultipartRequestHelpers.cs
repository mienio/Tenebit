using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Tenebit.Api.Http;
using Tenebit.Application.Assignments;
using Tenebit.Application.Common;

namespace Tenebit.Api.Endpoints;

internal static class MultipartRequestHelpers
{
    internal static readonly JsonSerializerOptions MultipartJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    internal static T? DeserializePart<T>(IFormCollection form, string name, out string? validationError)
    {
        validationError = null;
        var json = form[name].ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            validationError = $"Pole '{name}' jest wymagane.";
            return default;
        }

        if (json.Length > RequestLimits.Json)
        {
            validationError = ResultExtensions.Localize($"Pole '{name}' jest za duże.");
            return default;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(json, MultipartJsonOptions);
            if (value is null)
            {
                validationError = ResultExtensions.Localize($"Pole '{name}' ma nieprawidłowy format.");
                return default;
            }

            validationError = RequestObjectValidator.Validate(value);
            return validationError is null ? value : default;
        }
        catch (JsonException)
        {
            validationError = ResultExtensions.Localize($"Pole '{name}' ma nieprawidłowy JSON.");
            return default;
        }
    }

    internal static Dictionary<string, EvidenceManifestEntry>? DeserializeManifest(IFormCollection form, out string? validationError)
    {
        validationError = null;
        var json = form["evidenceManifest"].ToString();
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, EvidenceManifestEntry>();
        if (json.Length > RequestLimits.Json)
        {
            validationError = ResultExtensions.Localize("Manifest zdjęć jest za duży.");
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<Dictionary<string, EvidenceManifestEntry>>(json, MultipartJsonOptions)
                           ?? new Dictionary<string, EvidenceManifestEntry>();
            if (manifest.Count > RequestLimits.Dictionary)
            {
                validationError = ResultExtensions.Localize($"Manifest może zawierać maksymalnie {RequestLimits.Dictionary} pozycji.");
                return null;
            }

            foreach (var (key, entry) in manifest)
            {
                if (string.IsNullOrWhiteSpace(key) || key.Length > RequestLimits.Name)
                {
                    validationError = ResultExtensions.Localize("Manifest zawiera nieprawidłową nazwę pola pliku.");
                    return null;
                }
                var entryError = RequestObjectValidator.Validate(entry);
                if (entryError is not null)
                {
                    validationError = entryError;
                    return null;
                }
            }

            return manifest;
        }
        catch (JsonException)
        {
            validationError = ResultExtensions.Localize("Pole 'evidenceManifest' ma nieprawidłowy JSON.");
            return null;
        }
    }

    internal static void ValidateEvidenceBundle(IFormFileCollection files)
    {
        if (files.Count > RequestSizeLimits.MaxEvidenceBundleFiles)
            throw new BadHttpRequestException($"Można przesłać maksymalnie {RequestSizeLimits.MaxEvidenceBundleFiles} plików w jednym żądaniu.", StatusCodes.Status400BadRequest);

        long total = 0;
        foreach (var file in files)
        {
            if (file.Length <= 0) throw new BadHttpRequestException("Plik jest pusty.", StatusCodes.Status400BadRequest);
            if (file.Length > RequestSizeLimits.MaxEvidenceFileBytes)
                throw new BadHttpRequestException("Pojedyncze zdjęcie może mieć maksymalnie 5 MB.", StatusCodes.Status413PayloadTooLarge);
            total = checked(total + file.Length);
            if (total > RequestSizeLimits.MaxEvidenceBundleFileBytes)
                throw new BadHttpRequestException("Łączny rozmiar zdjęć w jednym żądaniu może wynosić maksymalnie 25 MB.", StatusCodes.Status413PayloadTooLarge);
        }
    }

    /// <summary>
    /// Reads the already-spooled IFormFile into the single byte[] required by the current DB/image APIs.
    /// Avoids MemoryStream + ToArray, which previously allocated a second full-size copy of every upload.
    /// </summary>
    internal static async Task<byte[]> ReadFileAsync(IFormFile file, long maxFileBytes, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new BadHttpRequestException("Plik jest pusty.", StatusCodes.Status400BadRequest);
        if (file.Length > maxFileBytes) throw new BadHttpRequestException("Plik jest za duży.", StatusCodes.Status413PayloadTooLarge);
        if (file.FileName.Length > 260) throw new BadHttpRequestException("Nazwa pliku jest za długa.", StatusCodes.Status400BadRequest);
        if (!string.IsNullOrEmpty(file.ContentType) && file.ContentType.Length > 160) throw new BadHttpRequestException("Typ pliku jest nieprawidłowy.", StatusCodes.Status400BadRequest);
        if (file.Name.Length > RequestLimits.Name) throw new BadHttpRequestException("Nazwa pola pliku jest za długa.", StatusCodes.Status400BadRequest);

        var content = new byte[checked((int)file.Length)];
        await using var stream = file.OpenReadStream();
        var offset = 0;
        while (offset < content.Length)
        {
            var read = await stream.ReadAsync(content.AsMemory(offset, content.Length - offset), cancellationToken);
            if (read == 0) break;
            offset += read;
        }

        if (offset != content.Length)
        {
            throw new BadHttpRequestException("Nie udało się odczytać całego pliku.", StatusCodes.Status400BadRequest);
        }

        return content;
    }

    internal const long MaxSingleEvidenceUploadBytes = RequestSizeLimits.MaxSingleEvidenceBodyBytes;
    internal const long MaxProcedureDocumentUploadBytes = RequestSizeLimits.MaxProcedureDocumentBodyBytes;
    internal const long MaxEvidenceBundleUploadBytes = RequestSizeLimits.MaxMultipartBodyBytes;

    internal static void LimitRequestBody(HttpRequest request, long maxBytes)
    {
        if (request.ContentLength is > 0 && request.ContentLength > maxBytes)
        {
            throw new BadHttpRequestException("Żądanie jest za duże.", StatusCodes.Status413PayloadTooLarge);
        }

        var feature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is not null && !feature.IsReadOnly)
        {
            feature.MaxRequestBodySize = maxBytes;
        }
    }
}

using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Evidence;

namespace Tenebit.Application.Evidence;

public sealed class AssetEvidenceService
{
    private const int MaxPerAssetAndPhase = 5;
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    private readonly IAssetEvidenceRepository _evidence;
    private readonly IAssetRepository _assets;
    private readonly IAssignmentRepository _assignments;
    private readonly IImageSanitizer _sanitizer;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Tenebit.Application.Assets.AssetAuthorizationService _assetAuthorization;

    public AssetEvidenceService(IAssetEvidenceRepository evidence, IAssetRepository assets, IAssignmentRepository assignments, IImageSanitizer sanitizer, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, Tenebit.Application.Assets.AssetAuthorizationService assetAuthorization)
    {
        _evidence = evidence;
        _assets = assets;
        _assignments = assignments;
        _sanitizer = sanitizer;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _assetAuthorization = assetAuthorization;
    }

    public async Task<Result<IReadOnlyList<AssetEvidenceResponse>>> ListByAssetAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<IReadOnlyList<AssetEvidenceResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assetAccess = await _assetAuthorization.EnsureCanViewAsync(assetId, cancellationToken);
        if (assetAccess.IsFailure) return Result<IReadOnlyList<AssetEvidenceResponse>>.Failure(assetAccess.Error!);

        var items = await _evidence.ListMetadataByAssetAsync(organizationId, assetId, cancellationToken);
        return Result<IReadOnlyList<AssetEvidenceResponse>>.Success(items.OrderByDescending(x => x.UploadedAt).Select(Map).ToList());
    }

    public async Task<Result<AssetEvidence>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<AssetEvidence>.Failure(access.Error!);

        var item = await _evidence.GetAsync(_currentUser.OrganizationId, id, cancellationToken);
        if (item is null) return Result<AssetEvidence>.Failure(Error.NotFound("Materiał dowodowy nie istnieje."));
        var assetAccess = await _assetAuthorization.EnsureCanViewAsync(item.AssetId, cancellationToken);
        return assetAccess.IsFailure ? Result<AssetEvidence>.Failure(Error.NotFound("Materiał dowodowy nie istnieje.")) : Result<AssetEvidence>.Success(item);
    }

    /// <summary>Publiczny odczyt zdjęcia wydania (spec 6.8). Zwraca wyłącznie materiał należący do
    /// konkretnego assignment i organizacji - brak izolacji zwracałby zdjęcia innych wydań.</summary>
    public async Task<Result<AssetEvidence>> GetPublicAssignmentEvidenceAsync(Guid organizationId, Guid assignmentId, Guid id, CancellationToken cancellationToken)
    {
        var assignment = await _assignments.GetAsync(organizationId, assignmentId, cancellationToken);
        if (assignment is null) return Result<AssetEvidence>.Failure(Error.NotFound("Wydanie nie istnieje."));

        var item = await _evidence.GetAsync(organizationId, id, cancellationToken);
        if (item is null || item.AssignmentId != assignmentId || item.Phase != EvidencePhase.Issue)
        {
            return Result<AssetEvidence>.Failure(Error.NotFound("Materiał dowodowy nie istnieje."));
        }

        return Result<AssetEvidence>.Success(item);
    }

    public async Task<Result<AssetEvidenceResponse>> UploadAsync(Guid assetId, UploadAssetEvidenceRequest request, string fileName, string? declaredContentType, byte[] content, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<AssetEvidenceResponse>.Failure(access.Error!);

        var requestError = RequestObjectValidator.Validate(request);
        if (requestError is not null) return Result<AssetEvidenceResponse>.Failure(Error.Validation(requestError));

        var validation = ValidateAndDetectFormat(declaredContentType, content);
        if (validation.IsFailure) return Result<AssetEvidenceResponse>.Failure(validation.Error!);

        SanitizedImage sanitized;
        try
        {
            sanitized = _sanitizer.StripMetadata(validation.Value, content);
        }
        catch (DomainException ex)
        {
            return Result<AssetEvidenceResponse>.Failure(Error.Validation(ex.Message));
        }

        var organizationId = _currentUser.OrganizationId;
        try
        {
            // Count + insert + SaveChanges are serialized per asset. This keeps the evidence limit check atomic
            // without blocking unrelated uploads and writes performed by other users in the same organization.
            return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "asset", [assetId], async ct =>
            {
                var asset = await _assets.GetAsync(organizationId, assetId, ct);
                if (asset is null) return Result<AssetEvidenceResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));

                if (request.AssignmentId.HasValue)
                {
                    var assignment = await _assignments.GetAsync(organizationId, request.AssignmentId.Value, ct);
                    if (assignment is null) return Result<AssetEvidenceResponse>.Failure(Error.NotFound("Wydanie nie istnieje."));
                    if (assignment.Assets.All(x => x.AssetId != assetId))
                    {
                        return Result<AssetEvidenceResponse>.Failure(Error.Validation("To aktywo nie należy do tego wydania."));
                    }
                }

                var existingCount = await _evidence.CountAsync(organizationId, assetId, request.Phase, ct);
                if (existingCount >= MaxPerAssetAndPhase)
                {
                    return Result<AssetEvidenceResponse>.Failure(Error.Validation("Osiągnięto limit 5 zdjęć dla tego aktywa i etapu."));
                }

                var item = new AssetEvidence(organizationId, assetId, request.AssignmentId, request.Phase, fileName, sanitized.ContentType, sanitized.Content, sanitized.Sha256, request.Caption, _currentUser.Subject, EvidenceUploadSource.AuthenticatedUser, _clock.UtcNow);
                _evidence.Add(item);
                _activity.Add(new ActivityLog(organizationId, "asset_evidence.uploaded", "asset_evidence", item.Id, _currentUser.Subject, item.FileName, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<AssetEvidenceResponse>.Success(Map(item));
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetEvidenceResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Upload zdjęcia przez publiczny token offboardingu (spec 4.6) - bez AccessPolicy/ICurrentUser,
    /// z tą samą walidacją formatu/rozmiaru/limitu i sanityzacją EXIF/GPS co <see cref="UploadAsync"/>.
    /// Organizacja/aktor pochodzą z już zweryfikowanego tokenu, nie z zalogowanego użytkownika.</summary>
    public async Task<Result<AssetEvidenceResponse>> UploadViaPublicTokenAsync(Guid organizationId, Guid assetId, Guid offboardingItemId,
        string fileName, string? declaredContentType, byte[] content, CancellationToken cancellationToken)
    {
        var validation = ValidateAndDetectFormat(declaredContentType, content);
        if (validation.IsFailure) return Result<AssetEvidenceResponse>.Failure(validation.Error!);

        SanitizedImage sanitized;
        try
        {
            sanitized = _sanitizer.StripMetadata(validation.Value, content);
        }
        catch (DomainException ex)
        {
            return Result<AssetEvidenceResponse>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "asset", [assetId], async ct =>
            {
                var asset = await _assets.GetAsync(organizationId, assetId, ct);
                if (asset is null) return Result<AssetEvidenceResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));

                var existingCount = await _evidence.CountAsync(organizationId, assetId, EvidencePhase.Offboarding, ct);
                if (existingCount >= MaxPerAssetAndPhase)
                {
                    return Result<AssetEvidenceResponse>.Failure(Error.Validation("Osiągnięto limit 5 zdjęć dla tego aktywa i etapu."));
                }

                var item = new AssetEvidence(organizationId, assetId, null, EvidencePhase.Offboarding, fileName, sanitized.ContentType, sanitized.Content, sanitized.Sha256, null, "employee", EvidenceUploadSource.PublicToken, _clock.UtcNow, offboardingItemId);
                _evidence.Add(item);
                _activity.Add(new ActivityLog(organizationId, "asset_evidence.uploaded", "asset_evidence", item.Id, "public-link", item.FileName, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<AssetEvidenceResponse>.Success(Map(item));
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetEvidenceResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Upload zdjęcia przez publiczny token kampanii audytu aktywów (spec 5.5) - analogicznie do
    /// <see cref="UploadViaPublicTokenAsync"/> dla offboardingu, ale z fazą Audit i AssetAuditItemId zamiast
    /// OffboardingItemId.</summary>
    public async Task<Result<AssetEvidenceResponse>> UploadViaAuditPublicTokenAsync(Guid organizationId, Guid assetId, Guid assetAuditItemId,
        string fileName, string? declaredContentType, byte[] content, CancellationToken cancellationToken)
    {
        var validation = ValidateAndDetectFormat(declaredContentType, content);
        if (validation.IsFailure) return Result<AssetEvidenceResponse>.Failure(validation.Error!);

        SanitizedImage sanitized;
        try
        {
            sanitized = _sanitizer.StripMetadata(validation.Value, content);
        }
        catch (DomainException ex)
        {
            return Result<AssetEvidenceResponse>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "asset", [assetId], async ct =>
            {
                var asset = await _assets.GetAsync(organizationId, assetId, ct);
                if (asset is null) return Result<AssetEvidenceResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));

                var existingCount = await _evidence.CountAsync(organizationId, assetId, EvidencePhase.Audit, ct);
                if (existingCount >= MaxPerAssetAndPhase)
                {
                    return Result<AssetEvidenceResponse>.Failure(Error.Validation("Osiągnięto limit 5 zdjęć dla tego aktywa i etapu."));
                }

                var item = new AssetEvidence(organizationId, assetId, null, EvidencePhase.Audit, fileName, sanitized.ContentType, sanitized.Content, sanitized.Sha256, null, "employee", EvidenceUploadSource.PublicToken, _clock.UtcNow, null, assetAuditItemId);
                _evidence.Add(item);
                _activity.Add(new ActivityLog(organizationId, "asset_evidence.uploaded", "asset_evidence", item.Id, "public-link", item.FileName, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return Result<AssetEvidenceResponse>.Success(Map(item));
            }, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetEvidenceResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>Przygotowuje partię zdjęć do zapisania W RAMACH transakcji zbiorczej (wydanie/zwrot ze zdjęciami).
    /// Najpierw waliduje i sanityzuje wszystkie pliki, a dopiero potem dodaje encje do repozytorium - dzięki temu
    /// błąd dowolnego pliku nie pozostawia częściowo zapisanego materiału. Nie wywołuje SaveChanges; zapisem
    /// (pojedynczym, atomowym) zarządza wywołujący serwis.</summary>
    public async Task<Result<IReadOnlyList<AssetEvidence>>> PrepareEvidenceBatchAsync(
        Guid organizationId, Guid? assignmentId, EvidencePhase phase, IReadOnlyList<EvidenceUploadInput> uploads, CancellationToken cancellationToken)
    {
        if (uploads.Count > 25)
            return Result<IReadOnlyList<AssetEvidence>>.Failure(Error.Validation("Można przesłać maksymalnie 25 zdjęć w jednym żądaniu."));
        long totalBytes = 0;
        foreach (var upload in uploads)
        {
            totalBytes = checked(totalBytes + upload.Content.LongLength);
            if (totalBytes > 25L * 1024 * 1024)
                return Result<IReadOnlyList<AssetEvidence>>.Failure(Error.Validation("Łączny rozmiar zdjęć w jednym żądaniu może wynosić maksymalnie 25 MB."));
        }

        var sanitized = new List<(EvidenceUploadInput Upload, SanitizedImage Image)>();
        var perAssetCount = new Dictionary<Guid, int>();

        foreach (var upload in uploads)
        {
            var validation = ValidateAndDetectFormat(upload.ContentType, upload.Content);
            if (validation.IsFailure) return Result<IReadOnlyList<AssetEvidence>>.Failure(validation.Error!);

            var asset = await _assets.GetAsync(organizationId, upload.AssetId, cancellationToken);
            if (asset is null) return Result<IReadOnlyList<AssetEvidence>>.Failure(Error.NotFound("Aktywo nie istnieje."));

            var existing = perAssetCount.TryGetValue(upload.AssetId, out var current)
                ? current
                : await _evidence.CountAsync(organizationId, upload.AssetId, phase, cancellationToken);
            if (existing >= MaxPerAssetAndPhase)
            {
                return Result<IReadOnlyList<AssetEvidence>>.Failure(Error.Validation("Osiągnięto limit 5 zdjęć dla tego aktywa i etapu."));
            }

            SanitizedImage image;
            try
            {
                image = _sanitizer.StripMetadata(validation.Value, upload.Content);
            }
            catch (DomainException ex)
            {
                return Result<IReadOnlyList<AssetEvidence>>.Failure(Error.Validation(ex.Message));
            }

            perAssetCount[upload.AssetId] = existing + 1;
            sanitized.Add((upload, image));
        }

        var items = new List<AssetEvidence>(uploads.Count);
        foreach (var entry in sanitized)
        {
            var upload = entry.Upload;
            var item = new AssetEvidence(organizationId, upload.AssetId, assignmentId, phase, upload.FileName, entry.Image.ContentType, entry.Image.Content, entry.Image.Sha256, upload.Caption, upload.UploadedBy, upload.UploadedVia, _clock.UtcNow);
            _evidence.Add(item);
            items.Add(item);
        }

        return Result<IReadOnlyList<AssetEvidence>>.Success(items);
    }

    private static Result<DetectedImageFormat> ValidateAndDetectFormat(string? declaredContentType, byte[] content)
    {
        if (!ImageSignature.IsAllowedContentType(declaredContentType))
        {
            return Result<DetectedImageFormat>.Failure(Error.Validation("Dozwolone są tylko zdjęcia w formacie JPEG, PNG lub WebP."));
        }

        if (content.Length == 0)
        {
            return Result<DetectedImageFormat>.Failure(Error.Validation("Zdjęcie jest puste."));
        }

        if (content.LongLength > MaxSizeBytes)
        {
            return Result<DetectedImageFormat>.Failure(Error.Validation("Zdjęcie może mieć maksymalnie 5 MB."));
        }

        var format = ImageSignature.Detect(content);
        return format == DetectedImageFormat.Unknown
            ? Result<DetectedImageFormat>.Failure(Error.Validation("Plik nie jest prawidłowym obrazem JPEG/PNG/WebP."))
            : Result<DetectedImageFormat>.Success(format);
    }

    public async Task<Result<AssetEvidenceResponse>> LockAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<AssetEvidenceResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var item = await _evidence.GetAsync(organizationId, id, cancellationToken);
        if (item is null) return Result<AssetEvidenceResponse>.Failure(Error.NotFound("Materiał dowodowy nie istnieje."));

        var wasLocked = item.LockedAt.HasValue;
        item.Lock(_clock.UtcNow);
        if (!wasLocked)
        {
            _activity.Add(new ActivityLog(organizationId, "asset_evidence.locked", "asset_evidence", item.Id, _currentUser.Subject, item.FileName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return Result<AssetEvidenceResponse>.Success(Map(item));
    }

    public async Task<Result<AssetEvidenceResponse>> SetLegalHoldAsync(Guid id, bool enabled, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<AssetEvidenceResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var item = await _evidence.GetAsync(organizationId, id, cancellationToken);
        if (item is null) return Result<AssetEvidenceResponse>.Failure(Error.NotFound("Materiał dowodowy nie istnieje."));

        var wasEnabled = item.LegalHold;
        item.SetLegalHold(enabled);
        if (wasEnabled != enabled)
        {
            var action = enabled ? "asset_evidence.legal_hold_set" : "asset_evidence.legal_hold_cleared";
            _activity.Add(new ActivityLog(organizationId, action, "asset_evidence", item.Id, _currentUser.Subject, item.FileName, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return Result<AssetEvidenceResponse>.Success(Map(item));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return access;

        var organizationId = _currentUser.OrganizationId;
        var item = await _evidence.GetAsync(organizationId, id, cancellationToken);
        if (item is null) return Result.Failure(Error.NotFound("Materiał dowodowy nie istnieje."));

        try
        {
            item.EnsureDeletable();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        _evidence.Remove(item);
        _activity.Add(new ActivityLog(organizationId, "asset_evidence.deleted", "asset_evidence", item.Id, _currentUser.Subject, item.FileName, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static AssetEvidenceResponse Map(AssetEvidence e) =>
        new(e.Id, e.AssetId, e.AssignmentId, e.Phase, e.FileName, e.ContentType, e.SizeBytes, e.Sha256, e.Caption, e.UploadedAt, e.UploadedBy, e.UploadedVia, e.LockedAt, e.LegalHold, e.RedactedAt);

    private static AssetEvidenceResponse Map(AssetEvidenceMetadata e) =>
        new(e.Id, e.AssetId, e.AssignmentId, e.Phase, e.FileName, e.ContentType, e.SizeBytes, e.Sha256, e.Caption, e.UploadedAt, e.UploadedBy, e.UploadedVia, e.LockedAt, e.LegalHold, e.RedactedAt);
}

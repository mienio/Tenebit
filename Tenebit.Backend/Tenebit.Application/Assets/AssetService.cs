using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Assets;

public sealed class AssetService
{
    private readonly IAssetRepository _assets;
    private readonly IAssetCategoryRepository _categories;
    private readonly IPersonRepository _people;
    private readonly ITeamRepository _teams;
    private readonly IActivityLogRepository _activity;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationUserRepository _organizationUsers;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly IAppLinkBuilder _linkBuilder;
    private readonly IEmailSender _emailSender;

    public AssetService(IAssetRepository assets, IAssetCategoryRepository categories, IPersonRepository people, ITeamRepository teams, IActivityLogRepository activity, ISubscriptionRepository subscriptions, IOrganizationRepository organizations, IOrganizationUserRepository organizationUsers, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IQrCodeGenerator qrCodeGenerator, IAppLinkBuilder linkBuilder, IEmailSender emailSender)
    {
        _assets = assets;
        _categories = categories;
        _people = people;
        _teams = teams;
        _activity = activity;
        _subscriptions = subscriptions;
        _organizations = organizations;
        _organizationUsers = organizationUsers;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _qrCodeGenerator = qrCodeGenerator;
        _linkBuilder = linkBuilder;
        _emailSender = emailSender;
    }

    public async Task<IReadOnlyList<AssetResponse>> ListAsync(string? search, AssetStatus? status, string? location, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var assets = await _assets.ListAsync(organizationId, search, status, location, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        return assets.Select(asset => Map(asset, categories, people, teams, _currentUser.Language)).ToList();
    }

    public async Task<Result<AssetResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var asset = await _assets.GetAsync(organizationId, id, cancellationToken);
        if (asset is null)
        {
            return Result<AssetResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        }

        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        return Result<AssetResponse>.Success(Map(asset, categories, people, teams, _currentUser.Language));
    }

    public async Task<Result<AssetResponse>> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<AssetResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;

            // Check subscription limits
            var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
            if (subscription is null)
            {
                subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
                _subscriptions.Add(subscription);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var currentAssets = await _assets.ListAsync(organizationId, null, null, null, cancellationToken);
            var limit = subscription.GetAssetLimit();

            if (currentAssets.Count >= limit)
            {
                var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
                return Result<AssetResponse>.Failure(Error.Validation($"Limit aktywów przekroczony. Plan {plan.Name} pozwala na {limit} aktywów. Przejdź na wyższy plan."));
            }

            var category = await _categories.GetAsync(organizationId, request.CategoryId, cancellationToken);
            if (category is null) return Result<AssetResponse>.Failure(Error.Validation("Wybrana kategoria nie istnieje."));
            if (await _assets.AssetTagExistsAsync(organizationId, request.AssetTag, null, cancellationToken))
            {
                return Result<AssetResponse>.Failure(Error.Conflict("Tag aktywa jest już używany."));
            }

            var customFieldsResult = ValidateCustomFields(category, request.CustomFields);
            if (customFieldsResult.IsFailure) return Result<AssetResponse>.Failure(customFieldsResult.Error!);

            var asset = new Asset(organizationId, request.CategoryId, request.Name, request.AssetTag);
            asset.UpdateCore(request.Name, request.AssetTag, request.SerialNumber, request.CategoryId, request.Location, request.Manufacturer, request.Model, request.PurchasePrice, request.Currency, request.PurchaseDate, request.WarrantyUntil, request.TeamId);
            asset.SetFieldValues(customFieldsResult.Value!);

            _assets.Add(asset);
            _activity.Add(new ActivityLog(organizationId, "asset.created", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetAsync(asset.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result<AssetResponse>> UpdateAsync(Guid id, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<AssetResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var asset = await _assets.GetAsync(organizationId, id, cancellationToken);
            if (asset is null) return Result<AssetResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
            var category = await _categories.GetAsync(organizationId, request.CategoryId, cancellationToken);
            if (category is null)
            {
                return Result<AssetResponse>.Failure(Error.Validation("Wybrana kategoria nie istnieje."));
            }

            if (await _assets.AssetTagExistsAsync(organizationId, request.AssetTag, id, cancellationToken))
            {
                return Result<AssetResponse>.Failure(Error.Conflict("Tag aktywa jest już używany."));
            }

            var mergedFields = PreserveUnchangedSensitiveFields(asset, category, request.CustomFields);
            var customFieldsResult = ValidateCustomFields(category, mergedFields);
            if (customFieldsResult.IsFailure) return Result<AssetResponse>.Failure(customFieldsResult.Error!);

            asset.UpdateCore(request.Name, request.AssetTag, request.SerialNumber, request.CategoryId, request.Location, request.Manufacturer, request.Model, request.PurchasePrice, request.Currency, request.PurchaseDate, request.WarrantyUntil, request.TeamId);
            asset.ChangeStatus(request.Status);
            asset.SetFieldValues(customFieldsResult.Value!);
            _activity.Add(new ActivityLog(organizationId, "asset.updated", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return await GetAsync(asset.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return access;

        var organizationId = _currentUser.OrganizationId;
        var asset = await _assets.GetAsync(organizationId, id, cancellationToken);
        if (asset is null) return Result.Failure(Error.NotFound("Aktywo nie istnieje."));

        _assets.Remove(asset);
        _activity.Add(new ActivityLog(organizationId, "asset.deleted", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<string>> GetQrSvgAsync(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        var asset = await _assets.GetAsync(organizationId, id, cancellationToken);
        if (asset is null) return Result<string>.Failure(Error.NotFound("Aktywo nie istnieje."));
        var scanLink = _linkBuilder.BuildAssetScanLink(organizationId, asset.Id);
        return Result<string>.Success(_qrCodeGenerator.CreateAssetQrSvg(scanLink));
    }

    public async Task<Result<PublicAssetScanResponse>> GetPublicScanAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
        if (asset is null) return Result<PublicAssetScanResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return Result<PublicAssetScanResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));
        return Result<PublicAssetScanResponse>.Success(new PublicAssetScanResponse(organization.Name));
    }

    public async Task<Result> ReportPublicIssueAsync(Guid organizationId, Guid assetId, ReportAssetIssueRequest request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
        if (asset is null) return Result.Failure(Error.NotFound("Aktywo nie istnieje."));
        if (string.IsNullOrWhiteSpace(request.Message)) return Result.Failure(Error.Validation("Treść zgłoszenia jest wymagana."));

        var users = await _organizationUsers.ListAsync(organizationId, cancellationToken);
        var adminEmails = users
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role is TenebitRoles.Owner or TenebitRoles.Admin))
            .Select(u => u.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var subject = $"Zgłoszenie ze skanu QR — {asset.Name} ({asset.AssetTag})";
        var html = $"""
            <p>Ktoś zeskanował kod QR aktywa <strong>{System.Net.WebUtility.HtmlEncode(asset.Name)}</strong> (tag: {System.Net.WebUtility.HtmlEncode(asset.AssetTag)}) i zgłosił:</p>
            <p>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>
            """;

        foreach (var email in adminEmails)
        {
            try { await _emailSender.SendAsync(email, subject, html, cancellationToken); } catch { /* best-effort */ }
        }

        _activity.Add(new ActivityLog(organizationId, "asset.scan_reported", "asset", asset.Id, "public-scan", asset.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static IReadOnlyDictionary<string, string> PreserveUnchangedSensitiveFields(Asset asset, AssetCategory category, IReadOnlyDictionary<string, string>? customFields)
    {
        var merged = new Dictionary<string, string>(customFields ?? new Dictionary<string, string>());
        foreach (var definition in category.FieldDefinitions.Where(x => x.FieldType == AssetFieldType.Sensitive))
        {
            var provided = merged.TryGetValue(definition.Key, out var value) ? value : null;
            if (!string.IsNullOrWhiteSpace(provided) && provided != SensitiveMask) continue;

            var existing = asset.FieldValues.FirstOrDefault(x => x.FieldKey == definition.Key)?.Value;
            if (existing is not null) merged[definition.Key] = existing;
            else merged.Remove(definition.Key);
        }

        return merged;
    }

    private static Result<Dictionary<string, string>> ValidateCustomFields(AssetCategory category, IReadOnlyDictionary<string, string>? customFields)
    {
        var values = new Dictionary<string, string>();
        foreach (var definition in category.FieldDefinitions)
        {
            var provided = customFields is not null && customFields.TryGetValue(definition.Key, out var value) ? value?.Trim() : null;
            if (definition.Required && string.IsNullOrWhiteSpace(provided))
            {
                return Result<Dictionary<string, string>>.Failure(Error.Validation($"Pole „{definition.Label}” jest wymagane."));
            }

            if (!string.IsNullOrWhiteSpace(provided))
            {
                values[definition.Key] = provided;
            }
        }

        return Result<Dictionary<string, string>>.Success(values);
    }

    private static AssetResponse Map(Asset asset, IReadOnlyList<AssetCategory> categories, IReadOnlyList<Person> people, IReadOnlyList<Team> teams, string language)
    {
        var category = categories.FirstOrDefault(x => x.Id == asset.CategoryId);
        var categoryName = category is null ? null : StarterAssetCategoryTranslations.TranslateName(category.IsSystem, language, category.Name);
        var assigned = asset.AssignedPersonId.HasValue ? people.FirstOrDefault(x => x.Id == asset.AssignedPersonId.Value) : null;
        var team = asset.TeamId.HasValue ? teams.FirstOrDefault(x => x.Id == asset.TeamId.Value) : null;
        var sensitiveKeys = category?.FieldDefinitions.Where(x => x.FieldType == AssetFieldType.Sensitive).Select(x => x.Key).ToHashSet() ?? [];
        var customFields = asset.FieldValues.ToDictionary(x => x.FieldKey, x => sensitiveKeys.Contains(x.FieldKey) ? SensitiveMask : x.Value);
        var fieldDefinitions = category?.FieldDefinitions
            .OrderBy(x => x.SortOrder)
            .Select(x => new AssetFieldDefinitionResponse(x.Id, x.Key, x.Label, x.FieldType, x.OptionList, x.Required))
            .ToList() ?? [];
        return new AssetResponse(asset.Id, asset.Name, asset.AssetTag, asset.SerialNumber, asset.CategoryId, categoryName, asset.Status, asset.AssignedPersonId, assigned?.FullName, asset.Location, asset.Manufacturer, asset.Model, asset.PurchasePrice, asset.Currency, asset.PurchaseDate, asset.WarrantyUntil, asset.QrCodePayload, asset.UpdatedAt, customFields, fieldDefinitions, asset.TeamId, team?.Name);
    }

    private const string SensitiveMask = "••••••••";

    public async Task<Result<string>> RevealSensitiveFieldAsync(Guid id, string fieldKey, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.LicenseManager);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var asset = await _assets.GetAsync(organizationId, id, cancellationToken);
        if (asset is null) return Result<string>.Failure(Error.NotFound("Aktywo nie istnieje."));

        var category = await _categories.GetAsync(organizationId, asset.CategoryId, cancellationToken);
        var definition = category?.FieldDefinitions.FirstOrDefault(x => x.Key == fieldKey);
        if (definition is null || definition.FieldType != AssetFieldType.Sensitive)
        {
            return Result<string>.Failure(Error.Validation("Pole nie jest polem wrażliwym."));
        }

        var value = asset.FieldValues.FirstOrDefault(x => x.FieldKey == fieldKey)?.Value ?? string.Empty;
        _activity.Add(new ActivityLog(organizationId, "asset.sensitive_field_revealed", "asset", asset.Id, _currentUser.Subject, definition.Label, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(value);
    }
}

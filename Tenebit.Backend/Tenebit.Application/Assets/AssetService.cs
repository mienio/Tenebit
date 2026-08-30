using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Abstractions.Repositories;
using Tenebit.Application.Common;
using Tenebit.Application.Identity;
using Tenebit.Application.Organizations;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.People;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Application.Assets;

public sealed class AssetService
{
    private readonly IAssetRepository _assets;
    private readonly IPublicReportThrottleRepository _throttle;
    private readonly IMaintenanceScheduleRepository _maintenance;
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
    private readonly IEmailOutboxWriter? _emailOutbox;
    private readonly ILogger<AssetService> _logger;
    private readonly IFieldEncryptor _fieldEncryptor;
    private readonly ManagerScopeService _managerScope;
    private readonly LocationReferenceResolver _locationResolver;

    // Roles in TenebitRoles.AssetViewers that see the whole organization; Manager alone is scoped to
    // its own team's assigned assets by ManagerScopeService (audyt AUD3-006).
    private static readonly string[] OrgWideRoles = [TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator, TenebitRoles.Technician, TenebitRoles.Hr, TenebitRoles.LicenseManager, TenebitRoles.Finance, TenebitRoles.Auditor];

    public AssetService(IAssetRepository assets, IPublicReportThrottleRepository throttle, IMaintenanceScheduleRepository maintenance, IAssetCategoryRepository categories, IPersonRepository people, ITeamRepository teams, IActivityLogRepository activity, ISubscriptionRepository subscriptions, IOrganizationRepository organizations, IOrganizationUserRepository organizationUsers, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IQrCodeGenerator qrCodeGenerator, IAppLinkBuilder linkBuilder, IEmailSender emailSender, ILogger<AssetService> logger, IFieldEncryptor fieldEncryptor, ManagerScopeService managerScope, LocationReferenceResolver locationResolver, IEmailOutboxWriter? emailOutbox = null)
    {
        _assets = assets;
        _throttle = throttle;
        _maintenance = maintenance;
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
        _emailOutbox = emailOutbox;
        _logger = logger;
        _fieldEncryptor = fieldEncryptor;
        _managerScope = managerScope;
        _locationResolver = locationResolver;
    }

    public async Task<Result<IReadOnlyList<AssetResponse>>> ListAsync(string? search, AssetStatus? status, string? location, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<IReadOnlyList<AssetResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        var assets = scope is null
            ? await _assets.ListAsync(organizationId, search, status, location, cancellationToken)
            : await _assets.ListScopedAsync(organizationId, search, status, location, scope.PersonIds, scope.TeamIds, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = scope is null
            ? await _people.ListAsync(organizationId, null, cancellationToken)
            : await _people.ListScopedAsync(organizationId, null, scope.PersonIds, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var maintenanceDue = await LoadMaintenanceDueAsync(organizationId, assets.Select(x => x.Id).ToArray(), cancellationToken);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        return Result<IReadOnlyList<AssetResponse>>.Success(assets.Select(asset => Map(asset, categories, people, teams, _currentUser.Language, maintenanceDue, today)).ToList());
    }

    public async Task<Result<PagedResult<AssetResponse>>> ListPagedAsync(string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, bool warrantyExpiring, string? sortKey, bool sortDesc, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<PagedResult<AssetResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        DateOnly? warrantyFrom = null;
        DateOnly? warrantyTo = null;
        if (warrantyExpiring)
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
            warrantyFrom = today;
            warrantyTo = today.AddDays(90);
        }

        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        var people = scope is null
            ? await _people.ListAsync(organizationId, null, cancellationToken)
            : await _people.ListScopedAsync(organizationId, null, scope.PersonIds, cancellationToken);
        var (items, total) = scope is null
            ? await _assets.ListPagedAsync(organizationId, search, status, location, teamId, categoryId, unassignedOnly, warrantyFrom, warrantyTo, sortKey, sortDesc, page, pageSize, cancellationToken)
            : await _assets.ListPagedScopedAsync(organizationId, search, status, location, teamId, categoryId, unassignedOnly, warrantyFrom, warrantyTo, sortKey, sortDesc, page, pageSize, scope.PersonIds, scope.TeamIds, cancellationToken);
        var maintenanceDue = await LoadMaintenanceDueAsync(organizationId, items.Select(x => x.Id).ToArray(), cancellationToken);
        var maintenanceToday = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        return Result<PagedResult<AssetResponse>>.Success(new PagedResult<AssetResponse>(items.Select(asset => Map(asset, categories, people, teams, _currentUser.Language, maintenanceDue, maintenanceToday)).ToList(), total, page, pageSize));
    }

    public async Task<Result<AssetGroupCountsResponse>> GetGroupCountsAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<AssetGroupCountsResponse>.Failure(access.Error!);

        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        var (byCategory, byStatus, byPerson) = scope is null
            ? await _assets.GetGroupCountsAsync(_currentUser.OrganizationId, cancellationToken)
            : await _assets.GetGroupCountsScopedAsync(_currentUser.OrganizationId, scope.PersonIds, scope.TeamIds, cancellationToken);
        return Result<AssetGroupCountsResponse>.Success(new AssetGroupCountsResponse(byCategory, byStatus, byPerson));
    }

    public async Task<Result<AssetResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<AssetResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var asset = await _assets.GetAsync(organizationId, id, cancellationToken);
        if (asset is null)
        {
            return Result<AssetResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        }

        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        if (scope is not null && !scope.ContainsAsset(asset.AssignedPersonId, asset.TeamId))
        {
            return Result<AssetResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        }

        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = scope is null
            ? await _people.ListAsync(organizationId, null, cancellationToken)
            : await _people.ListScopedAsync(organizationId, null, scope.PersonIds, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var maintenanceDue = await LoadMaintenanceDueAsync(organizationId, [asset.Id], cancellationToken);
        return Result<AssetResponse>.Success(Map(asset, categories, people, teams, _currentUser.Language, maintenanceDue, DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime)));
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

            var limit = subscription.GetAssetLimit();

            var category = await _categories.GetAsync(organizationId, request.CategoryId, cancellationToken);
            if (category is null) return Result<AssetResponse>.Failure(Error.Validation("Wybrana kategoria nie istnieje."));
            if (await _assets.AssetTagExistsAsync(organizationId, request.AssetTag, null, cancellationToken))
            {
                return Result<AssetResponse>.Failure(Error.Conflict("Tag aktywa jest już używany."));
            }

            if (request.TeamId.HasValue && await _teams.GetAsync(organizationId, request.TeamId.Value, cancellationToken) is null)
            {
                return Result<AssetResponse>.Failure(Error.Validation("Wybrany zespół nie istnieje."));
            }

            var customFieldsResult = ValidateCustomFields(category, request.CustomFields);
            if (customFieldsResult.IsFailure) return Result<AssetResponse>.Failure(customFieldsResult.Error!);
            var locationResult = await _locationResolver.ResolveAsync(organizationId, request.Location, cancellationToken);
            if (locationResult.IsFailure) return Result<AssetResponse>.Failure(locationResult.Error!);

            var asset = new Asset(organizationId, request.CategoryId, request.Name, request.AssetTag);
            await EnsureUniqueScanCodeAsync(asset, cancellationToken);
            asset.UpdateCore(request.Name, request.AssetTag, request.SerialNumber, request.CategoryId, locationResult.Value!.FullPath, request.Manufacturer, request.Model, request.PurchasePrice, request.Currency, request.PurchaseDate, request.WarrantyUntil, request.TeamId);
            asset.SetLocation(locationResult.Value.Id, locationResult.Value.FullPath);
            asset.SetFieldValues(EncryptSensitiveFields(category, customFieldsResult.Value!));

            // Serializujemy wyłącznie operacje zużywające limit aktywów. Nie blokujemy uploadów,
            // refreshy ani innych zapisów całej organizacji, a check-then-insert nadal pozostaje atomowy.
            var withinLimit = await _unitOfWork.ExecuteWithResourceLocksAsync(
                organizationId,
                "asset-capacity",
                [organizationId],
                async ct =>
            {
                var currentCount = await _assets.CountAsync(organizationId, ct);
                if (currentCount >= limit) return false;

                _assets.Add(asset);
                _activity.Add(new ActivityLog(organizationId, "asset.created", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
                await _unitOfWork.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);

            if (!withinLimit)
            {
                var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
                return Result<AssetResponse>.Failure(Error.Validation($"Limit aktywów przekroczony. Plan {plan.Name} pozwala na {limit} aktywów. Przejdź na wyższy plan."));
            }

            return await GetAsync(asset.Id, cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result<AssetResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    public const int MaxBatchQuantity = 100;
    private const int MaxTagPadding = 8;

    /// <summary>
    /// Creates a run of identical assets in one transaction.
    ///
    /// The whole run is validated - every generated tag, the plan's remaining capacity - before anything
    /// is written, and then written under the same capacity lock a single create uses. A partial batch is
    /// worse than a rejected one here: the operator would have to work out which of the twenty tags made
    /// it in before retrying, and the missing ones are exactly the labels already stuck to the boxes.
    /// </summary>
    public async Task<Result<CreateAssetBatchResponse>> CreateBatchAsync(CreateAssetBatchRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<CreateAssetBatchResponse>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;

            if (request.Quantity < 1 || request.Quantity > MaxBatchQuantity)
            {
                return Result<CreateAssetBatchResponse>.Failure(Error.Validation($"Liczba sztuk musi mieścić się w zakresie 1-{MaxBatchQuantity}."));
            }

            var prefix = request.TagPrefix.Trim();
            if (prefix.Length == 0) return Result<CreateAssetBatchResponse>.Failure(Error.Validation("Prefiks tagu jest wymagany."));
            if (request.TagPadding is < 0 or > MaxTagPadding)
            {
                return Result<CreateAssetBatchResponse>.Failure(Error.Validation($"Liczba cyfr numeracji musi mieścić się w zakresie 0-{MaxTagPadding}."));
            }

            if (request.TagStartNumber < 0 || request.TagStartNumber > 999_999)
            {
                return Result<CreateAssetBatchResponse>.Failure(Error.Validation("Numer początkowy musi mieścić się w zakresie 0-999999."));
            }

            var serials = (request.SerialNumbers ?? [])
                .Select(serial => serial.Trim())
                .Where(serial => serial.Length > 0)
                .ToList();
            if (serials.Count > request.Quantity)
            {
                return Result<CreateAssetBatchResponse>.Failure(Error.Validation("Podano więcej numerów seryjnych niż sztuk w partii."));
            }

            var tags = BuildBatchTags(prefix, request.TagStartNumber, request.TagPadding, request.Quantity);
            var tooLong = tags.FirstOrDefault(tag => tag.Length > 80);
            if (tooLong is not null)
            {
                return Result<CreateAssetBatchResponse>.Failure(Error.Validation($"Tag '{tooLong}' przekracza 80 znaków. Skróć prefiks."));
            }

            var category = await _categories.GetAsync(organizationId, request.CategoryId, cancellationToken);
            if (category is null) return Result<CreateAssetBatchResponse>.Failure(Error.Validation("Wybrana kategoria nie istnieje."));

            if (request.TeamId.HasValue && await _teams.GetAsync(organizationId, request.TeamId.Value, cancellationToken) is null)
            {
                return Result<CreateAssetBatchResponse>.Failure(Error.Validation("Wybrany zespół nie istnieje."));
            }

            var customFieldsResult = ValidateCustomFields(category, request.CustomFields);
            if (customFieldsResult.IsFailure) return Result<CreateAssetBatchResponse>.Failure(customFieldsResult.Error!);
            var locationResult = await _locationResolver.ResolveAsync(organizationId, request.Location, cancellationToken);
            if (locationResult.IsFailure) return Result<CreateAssetBatchResponse>.Failure(locationResult.Error!);

            var taken = new List<string>();
            foreach (var tag in tags)
            {
                if (await _assets.AssetTagExistsAsync(organizationId, tag, null, cancellationToken)) taken.Add(tag);
                if (taken.Count == 5) break;
            }

            if (taken.Count > 0)
            {
                return Result<CreateAssetBatchResponse>.Failure(Error.Conflict($"Te tagi są już używane: {string.Join(", ", taken)}. Zmień numer początkowy lub prefiks."));
            }

            var encryptedFields = EncryptSensitiveFields(category, customFieldsResult.Value!);
            var created = new List<Asset>(tags.Count);
            for (var i = 0; i < tags.Count; i++)
            {
                var asset = new Asset(organizationId, request.CategoryId, request.Name, tags[i]);
                await EnsureUniqueScanCodeAsync(asset, cancellationToken, created);
                asset.UpdateCore(request.Name, tags[i], i < serials.Count ? serials[i] : null, request.CategoryId, locationResult.Value!.FullPath, request.Manufacturer, request.Model, request.PurchasePrice, request.Currency, request.PurchaseDate, request.WarrantyUntil, request.TeamId);
                asset.SetLocation(locationResult.Value.Id, locationResult.Value.FullPath);
                asset.SetFieldValues(encryptedFields);
                created.Add(asset);
            }

            var subscription = await _subscriptions.GetByOrganizationAsync(organizationId, cancellationToken);
            if (subscription is null)
            {
                subscription = new OrganizationSubscription(organizationId, SubscriptionPlan.Free.Key);
                _subscriptions.Add(subscription);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var limit = subscription.GetAssetLimit();
            var remaining = 0;
            var withinLimit = await _unitOfWork.ExecuteWithResourceLocksAsync(
                organizationId,
                "asset-capacity",
                [organizationId],
                async ct =>
                {
                    var currentCount = await _assets.CountAsync(organizationId, ct);
                    remaining = Math.Max(0, limit - currentCount);
                    if (currentCount + created.Count > limit) return false;

                    foreach (var asset in created)
                    {
                        _assets.Add(asset);
                        _activity.Add(new ActivityLog(organizationId, "asset.created", "asset", asset.Id, _currentUser.Subject, asset.Name, _clock.UtcNow));
                    }

                    await _unitOfWork.SaveChangesAsync(ct);
                    return true;
                }, cancellationToken);

            if (!withinLimit)
            {
                var plan = SubscriptionPlan.FromKey(subscription.PlanKey) ?? SubscriptionPlan.Free;
                return Result<CreateAssetBatchResponse>.Failure(Error.Validation($"Limit aktywów przekroczony. Plan {plan.Name} pozwala na {limit} aktywów, zostało wolnych: {remaining}. Przejdź na wyższy plan lub zmniejsz partię."));
            }

            var categories = await _categories.ListAsync(organizationId, cancellationToken);
            var teams = await _teams.ListAsync(organizationId, cancellationToken);
            var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
            var responses = created.Select(asset => Map(asset, categories, [], teams, _currentUser.Language, null, today)).ToList();
            return Result<CreateAssetBatchResponse>.Success(new CreateAssetBatchResponse(responses.Count, responses));
        }
        catch (DomainException ex)
        {
            return Result<CreateAssetBatchResponse>.Failure(Error.Validation(ex.Message));
        }
    }

    /// <summary>
    /// Redraws the asset's code until it is free. Fifty bits make a clash astronomically unlikely, but
    /// "unlikely" is not "impossible" and the column is unique, so the alternative to checking is an
    /// occasional insert that fails for a reason nobody would recognise. Within a batch the codes are
    /// also checked against each other, since none of them is in the database yet.
    /// </summary>
    private async Task EnsureUniqueScanCodeAsync(Asset asset, CancellationToken cancellationToken, IReadOnlyCollection<Asset>? pending = null)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var clashesInBatch = pending is not null && pending.Any(other => other.ScanCode == asset.ScanCode);
            if (!clashesInBatch && !await _assets.ScanCodeExistsAsync(asset.ScanCode, cancellationToken)) return;
            asset.RegenerateScanCode();
        }

        throw new DomainException("Nie udało się wygenerować unikalnego kodu etykiety. Spróbuj ponownie.");
    }

    /// <summary>
    /// Resolves a scanned label for a signed-in user, so the app can open the asset it belongs to.
    /// Scoped to the caller's organization: a code from another tenant's sticker must look exactly like
    /// a code that does not exist.
    /// </summary>
    public async Task<Result<Guid>> ResolveScanCodeAsync(string scanCode, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<Guid>.Failure(access.Error!);

        var asset = AssetScanCode.IsWellFormed(scanCode) ? await _assets.FindByScanCodeAsync(scanCode, cancellationToken) : null;
        return asset is null || asset.OrganizationId != _currentUser.OrganizationId
            ? Result<Guid>.Failure(Error.NotFound("Aktywo nie istnieje."))
            : Result<Guid>.Success(asset.Id);
    }

    public async Task<Result<PublicAssetScanResponse>> GetPublicScanByCodeAsync(string scanCode, CancellationToken cancellationToken)
    {
        var asset = AssetScanCode.IsWellFormed(scanCode) ? await _assets.FindByScanCodeAsync(scanCode, cancellationToken) : null;
        if (asset is null) return Result<PublicAssetScanResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        return await GetPublicScanAsync(asset.OrganizationId, asset.Id, cancellationToken);
    }

    public async Task<Result> ReportPublicIssueByCodeAsync(string scanCode, ReportAssetIssueRequest request, CancellationToken cancellationToken)
    {
        var asset = AssetScanCode.IsWellFormed(scanCode) ? await _assets.FindByScanCodeAsync(scanCode, cancellationToken) : null;
        if (asset is null) return Result.Failure(Error.NotFound("Aktywo nie istnieje."));
        return await ReportPublicIssueAsync(asset.OrganizationId, asset.Id, request, cancellationToken);
    }

    private static List<string> BuildBatchTags(string prefix, int startNumber, int padding, int quantity)
    {
        var tags = new List<string>(quantity);
        for (var i = 0; i < quantity; i++)
        {
            var number = (startNumber + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
            tags.Add(prefix + (padding > 0 ? number.PadLeft(padding, '0') : number));
        }
        return tags;
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

            if (request.TeamId.HasValue && await _teams.GetAsync(organizationId, request.TeamId.Value, cancellationToken) is null)
            {
                return Result<AssetResponse>.Failure(Error.Validation("Wybrany zespół nie istnieje."));
            }

            var mergedFields = PreserveUnchangedSensitiveFields(asset, category, request.CustomFields);
            var customFieldsResult = ValidateCustomFields(category, mergedFields);
            if (customFieldsResult.IsFailure) return Result<AssetResponse>.Failure(customFieldsResult.Error!);
            var locationResult = await _locationResolver.ResolveAsync(organizationId, request.Location, cancellationToken);
            if (locationResult.IsFailure) return Result<AssetResponse>.Failure(locationResult.Error!);

            asset.UpdateCore(request.Name, request.AssetTag, request.SerialNumber, request.CategoryId, locationResult.Value!.FullPath, request.Manufacturer, request.Model, request.PurchasePrice, request.Currency, request.PurchaseDate, request.WarrantyUntil, request.TeamId);
            asset.SetLocation(locationResult.Value!.Id, locationResult.Value.FullPath);
            asset.ChangeStatus(request.Status);
            asset.SetFieldValues(EncryptSensitiveFields(category, customFieldsResult.Value!));
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
        if (await _assets.IsUsedAsync(organizationId, id, cancellationToken))
        {
            return Result.Failure(Error.Conflict("Nie można usunąć aktywa powiązanego z wydaniami, kontrolami, zgłoszeniami serwisowymi, rezerwacjami lub offboardingiem."));
        }

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
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var scanLink = _linkBuilder.BuildAssetScanLink(asset.ScanCode);
        var content = organization is null
            ? new QrLabelContent([], [asset.AssetTag, asset.Name], null)
            : QrLabelComposer.Compose(organization, asset.Name, asset.AssetTag, asset.SerialNumber);
        return Result<string>.Success(_qrCodeGenerator.CreateLabelledAssetQrSvg(scanLink, content));
    }

    public async Task<Result<PublicAssetScanResponse>> GetPublicScanAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken)
    {
        // Jedna odpowiedź na oba braki. Rozróżnienie "aktywo nie istnieje" od "organizacja nie istnieje"
        // było oracle'em: pozwalało anonimowemu klientowi potwierdzić, że dany identyfikator organizacji
        // jest prawdziwy, zanim trafił na poprawną parę z etykiety QR.
        var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
        var organization = asset is null ? null : await _organizations.GetAsync(organizationId, cancellationToken);
        if (asset is null || organization is null)
        {
            return Result<PublicAssetScanResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        }

        return Result<PublicAssetScanResponse>.Success(new PublicAssetScanResponse(organization.Name));
    }

    /// <summary>
    /// Three limits, because one number cannot serve two purposes. The per-reporter cooldown stops one
    /// person re-reporting the same asset; the per-asset cap protects the admins' inbox no matter how
    /// many people (or addresses) are involved; the per-reporter cap stops someone walking a floor and
    /// scanning every label. The old single limit conflated these and, keyed on a constant actor, let
    /// one report silence everybody else on that asset.
    /// </summary>
    private static readonly TimeSpan PublicIssueReportCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PublicIssueBurstWindow = TimeSpan.FromHours(1);
    private const int PublicIssueMaxPerAsset = 3;
    private const int PublicIssueMaxPerReporter = 10;

    public async Task<Result> ReportPublicIssueAsync(Guid organizationId, Guid assetId, ReportAssetIssueRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return Result.Failure(Error.Validation("Treść zgłoszenia jest wymagana."));

        // Ten sam brak rozróżnienia co w GetPublicScanAsync - odpowiedź nie może zdradzać, który
        // z dwóch identyfikatorów był poprawny.
        var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
        var organization = asset is null ? null : await _organizations.GetAsync(organizationId, cancellationToken);
        if (asset is null || organization is null) return Result.Failure(Error.NotFound("Aktywo nie istnieje."));

        return await _unitOfWork.ExecuteWithResourceLocksAsync(organizationId, "asset-public-issue", [assetId], async ct =>
        {
            var now = _clock.UtcNow;
            var capturedIp = PublicIpPrivacyPolicy.Capture(organization, _currentUser.IpAddress, now);
            const string actorSubject = "public-scan";

            // Derived from the raw address regardless of the organization's IP retention setting: the
            // limit must not weaken because a tenant chose not to store addresses. Nothing reversible
            // is written - see PublicReporterKey.
            var reporter = PublicReporterKey.Derive(organizationId, _currentUser.IpAddress);
            var burstSince = now - PublicIssueBurstWindow;

            // The per-asset advisory lock makes the limit checks + enqueue + audit write atomic. Two concurrent
            // public scans can no longer both observe an empty window and send duplicate notifications.
            var sameReporterRecently = await _throttle.ExistsForReporterAndAssetAsync(organizationId, asset.Id, reporter, now - PublicIssueReportCooldown, ct);
            if (sameReporterRecently) return Result.Failure(Error.TooManyRequests("To aktywo zostało już przez Ciebie zgłoszone niedawno. Spróbuj ponownie później."));

            var reportsForAsset = await _throttle.CountForAssetAsync(organizationId, asset.Id, burstSince, ct);
            if (reportsForAsset >= PublicIssueMaxPerAsset) return Result.Failure(Error.TooManyRequests("To aktywo zostało już zgłoszone wielokrotnie. Zgłoszenie dotarło do administratorów."));

            var reportsFromReporter = await _throttle.CountForReporterAsync(organizationId, reporter, burstSince, ct);
            if (reportsFromReporter >= PublicIssueMaxPerReporter) return Result.Failure(Error.TooManyRequests("Zbyt wiele zgłoszeń w krótkim czasie. Spróbuj ponownie później."));

            var users = await _organizationUsers.ListAsync(organizationId, ct);
            var adminEmails = users
                .Where(user => user.IsActive && user.Roles.Any(role => role.Role is TenebitRoles.Owner or TenebitRoles.Admin))
                .Select(user => user.Email)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var subject = $"Zgłoszenie ze skanu QR - {asset.Name} ({asset.AssetTag})";
            var html = $"""
                <p>Ktoś zeskanował kod QR aktywa <strong>{System.Net.WebUtility.HtmlEncode(asset.Name)}</strong> (tag: {System.Net.WebUtility.HtmlEncode(asset.AssetTag)}) i zgłosił:</p>
                <p>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>
                """;

            foreach (var email in adminEmails)
            {
                try
                {
                    if (_emailOutbox is not null)
                    {
                        var recipientHash = TokenHasher.Hash(email.ToLowerInvariant());
                        await _emailOutbox.EnqueueAsync(organizationId, email, subject, html, "asset-public-issue", $"asset-public-issue:{asset.Id:N}:{now.ToUnixTimeSeconds() / 600}:{recipientHash}", ct);
                    }
                    else
                    {
                        await _emailSender.SendAsync(email, subject, html, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nie udało się zakolejkować powiadomienia ze skanu QR dla aktywa {AssetId}", asset.Id);
                }
            }

            _activity.Add(new ActivityLog(organizationId, "asset.scan_reported", "asset", asset.Id, actorSubject, asset.Name, now, capturedIp.StoredIp, capturedIp.ExpiresAt));
            _throttle.Add(new PublicReportThrottle(organizationId, asset.Id, reporter, now));
            // Opportunistic cleanup: nothing older than the widest window is ever read again, so the
            // table stays small without a background job to own.
            await _throttle.PurgeOlderThanAsync(organizationId, now - PublicIssueBurstWindow - TimeSpan.FromHours(1), ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }

    // Zwraca zawsze plaintext (istniejąca wartość jest odszyfrowywana) - pojedynczy punkt szyfrowania to
    // EncryptSensitiveFields wołane po ValidateCustomFields, żeby zachowane-bez-zmian wartości nie zostały
    // zaszyfrowane drugi raz.
    private IReadOnlyDictionary<string, string> PreserveUnchangedSensitiveFields(Asset asset, AssetCategory category, IReadOnlyDictionary<string, string>? customFields)
    {
        var merged = new Dictionary<string, string>(customFields ?? new Dictionary<string, string>());
        foreach (var definition in category.FieldDefinitions.Where(x => x.FieldType == AssetFieldType.Sensitive))
        {
            var provided = merged.TryGetValue(definition.Key, out var value) ? value : null;
            if (!string.IsNullOrWhiteSpace(provided) && provided != SensitiveMask) continue;

            var existing = asset.FieldValues.FirstOrDefault(x => x.FieldKey == definition.Key)?.Value;
            if (existing is not null) merged[definition.Key] = _fieldEncryptor.Decrypt(FieldEncryptionPurposes.AssetSensitiveField, existing);
            else merged.Remove(definition.Key);
        }

        return merged;
    }

    /// <summary>Szyfruje wartości pól typu Sensitive tuż przed zapisem (audyt P1.5) - wołane po
    /// ValidateCustomFields, więc operuje na finalnym, przyciętym zbiorze wartości.</summary>
    private Dictionary<string, string> EncryptSensitiveFields(AssetCategory category, Dictionary<string, string> values)
    {
        foreach (var definition in category.FieldDefinitions.Where(x => x.FieldType == AssetFieldType.Sensitive))
        {
            if (values.TryGetValue(definition.Key, out var plain))
            {
                values[definition.Key] = _fieldEncryptor.Encrypt(FieldEncryptionPurposes.AssetSensitiveField, plain);
            }
        }

        return values;
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

    private static AssetResponse Map(Asset asset, IReadOnlyList<AssetCategory> categories, IReadOnlyList<Person> people, IReadOnlyList<Team> teams, string language, IReadOnlyDictionary<Guid, DateOnly>? maintenanceDue = null, DateOnly? today = null)
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
        return new AssetResponse(asset.Id, asset.Name, asset.AssetTag, asset.SerialNumber, asset.CategoryId, categoryName, asset.Status, asset.AssignedPersonId, assigned?.FullName, asset.Location, asset.Manufacturer, asset.Model, asset.PurchasePrice, asset.Currency, asset.PurchaseDate, asset.WarrantyUntil, asset.QrCodePayload, asset.UpdatedAt, customFields, fieldDefinitions, asset.TeamId, team?.Name, MaintenanceStatusOf(asset.Id, maintenanceDue, today));
    }

    /// <summary>Best-effort: a maintenance lookup failure must never take down the asset list itself.</summary>
    private async Task<IReadOnlyDictionary<Guid, DateOnly>?> LoadMaintenanceDueAsync(Guid organizationId, IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken)
    {
        try
        {
            return await _maintenance.GetEarliestDueByAssetAsync(organizationId, assetIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nie udało się pobrać terminów przeglądów dla listy aktywów.");
            return null;
        }
    }

    /// <summary>
    /// Buckets the earliest upcoming maintenance into what the edge indicator needs. Thresholds match
    /// the maintenance list so the same asset never looks urgent in one place and calm in the other.
    /// </summary>
    private static string MaintenanceStatusOf(Guid assetId, IReadOnlyDictionary<Guid, DateOnly>? due, DateOnly? today)
    {
        if (due is null || today is not { } reference || !due.TryGetValue(assetId, out var nextDue)) return "none";

        var daysRemaining = nextDue.DayNumber - reference.DayNumber;
        if (daysRemaining < 0) return "overdue";   // black  - the date has passed
        if (daysRemaining <= 7) return "due";      // red    - act this week
        if (daysRemaining <= 30) return "soon";    // orange - worth planning
        return "ok";                               // green  - nothing to do
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

        var stored = asset.FieldValues.FirstOrDefault(x => x.FieldKey == fieldKey)?.Value ?? string.Empty;
        var value = stored.Length == 0 ? stored : _fieldEncryptor.Decrypt(FieldEncryptionPurposes.AssetSensitiveField, stored);
        _activity.Add(new ActivityLog(organizationId, "asset.sensitive_field_revealed", "asset", asset.Id, _currentUser.Subject, definition.Label, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(value);
    }

    /// <summary>Eksport listy aktywów do JSON. Zawiera wyłącznie dane z <see cref="AssetResponse"/> - bez materiałów dowodowych (AssetEvidence),
    /// które są danymi własnościowymi organizacji i nie podlegają eksportowi.</summary>
    /// <summary>
    /// The same rows the list would show, for the same filters and in the same order.
    ///
    /// Export previously accepted only search/status/location, so a screen narrowed by team, owner or
    /// warranty silently exported far more than it displayed. Both exports now go through the paged
    /// query the list itself uses, which keeps the two definitions of "what is on screen" from drifting.
    /// </summary>
    private async Task<(IReadOnlyList<Asset> Assets, IReadOnlyList<AssetCategory> Categories, IReadOnlyList<Domain.People.Person> People, IReadOnlyList<Team> Teams)> LoadForExportAsync(
        string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, bool warrantyExpiring, string? sortKey, bool sortDesc, CancellationToken cancellationToken)
    {
        var organizationId = _currentUser.OrganizationId;
        DateOnly? warrantyFrom = null;
        DateOnly? warrantyTo = null;
        if (warrantyExpiring)
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
            warrantyFrom = today;
            warrantyTo = today.AddDays(90);
        }

        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        var (items, _) = scope is null
            ? await _assets.ListPagedAsync(organizationId, search, status, location, teamId, categoryId, unassignedOnly, warrantyFrom, warrantyTo, sortKey, sortDesc, 1, int.MaxValue, cancellationToken)
            : await _assets.ListPagedScopedAsync(organizationId, search, status, location, teamId, categoryId, unassignedOnly, warrantyFrom, warrantyTo, sortKey, sortDesc, 1, int.MaxValue, scope.PersonIds, scope.TeamIds, cancellationToken);

        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = scope is null
            ? await _people.ListAsync(organizationId, null, cancellationToken)
            : await _people.ListScopedAsync(organizationId, null, scope.PersonIds, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        return (items, categories, people, teams);
    }

    public async Task<Result<string>> ExportJsonAsync(string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, bool warrantyExpiring, string? sortKey, bool sortDesc, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var (assets, categories, people, teams) = await LoadForExportAsync(search, status, location, teamId, categoryId, unassignedOnly, warrantyExpiring, sortKey, sortDesc, cancellationToken);
        var language = _currentUser.Language;
        var payload = assets.Select(asset => Map(asset, categories, people, teams, language)).ToList();
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return Result<string>.Success(json);
    }

    /// <summary>Eksport listy aktywów do CSV. Dane identyczne jak <see cref="ExportJsonAsync"/> (bez AssetEvidence). Status
    /// eksportowany jako nazwa enuma - brak dedykowanego helpera translacji w warstwie Application.</summary>
    /// <summary>
    /// Book value of the fleet today. Finance-facing, so it is gated to the roles that already see asset
    /// values, and it respects manager scope exactly like the asset list does.
    /// </summary>
    public async Task<Result<FleetValueResponse>> GetFleetValueAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<FleetValueResponse>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var scope = await _managerScope.ResolveAsync(_currentUser, OrgWideRoles, cancellationToken);
        var assets = scope is null
            ? await _assets.ListAsync(organizationId, null, null, null, cancellationToken)
            : await _assets.ListScopedAsync(organizationId, null, null, null, scope.PersonIds, scope.TeamIds, cancellationToken);

        var categories = (await _categories.ListAsync(organizationId, cancellationToken)).ToDictionary(x => x.Id);
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var slices = new Dictionary<Guid, CategoryAccumulator>();
        decimal totalPurchase = 0m, totalCurrent = 0m;
        int withValue = 0, withoutPrice = 0;

        foreach (var asset in assets)
        {
            categories.TryGetValue(asset.CategoryId, out var category);
            var book = DepreciationCalculator.Calculate(asset.PurchasePrice, asset.PurchaseDate, category?.DepreciationMonths, today);
            if (book is null)
            {
                withoutPrice++;
                continue;
            }

            withValue++;
            totalPurchase += book.PurchasePrice;
            totalCurrent += book.CurrentValue;

            var name = category?.Name ?? "—";
            var current = slices.TryGetValue(asset.CategoryId, out var existing)
                ? existing
                : new CategoryAccumulator(name, category?.DepreciationMonths, 0, 0m, 0m);
            slices[asset.CategoryId] = current with
            {
                Count = current.Count + 1,
                Purchase = current.Purchase + book.PurchasePrice,
                Current = current.Current + book.CurrentValue,
            };
        }

        var byCategory = slices
            .Select(pair => new CategoryValueSlice(pair.Key, pair.Value.Name, pair.Value.Months, pair.Value.Count, pair.Value.Purchase, pair.Value.Current))
            .OrderByDescending(x => x.CurrentValue)
            .ToArray();

        return Result<FleetValueResponse>.Success(new FleetValueResponse(
            totalPurchase, totalCurrent, totalPurchase - totalCurrent,
            withValue, withoutPrice, organization?.Currency ?? "PLN", byCategory));
    }

    /// <summary>
    /// CSV columns in fixed order. Keys match the front-end column picker, so an export contains exactly
    /// the columns the user chose to see rather than everything the record happens to hold.
    /// </summary>
    private static readonly (string Key, string Header, Func<AssetResponse, string> Value)[] ExportColumns =
    [
        ("name", "Nazwa", a => a.Name),
        ("assetTag", "Tag", a => a.AssetTag),
        ("serialNumber", "Numer seryjny", a => a.SerialNumber ?? ""),
        ("category", "Kategoria", a => a.CategoryName ?? ""),
        ("status", "Status", a => a.Status.ToString()),
        ("person", "Osoba", a => a.AssignedPersonName ?? ""),
        ("location", "Lokalizacja", a => a.Location ?? ""),
        ("team", "Zespół", a => a.TeamName ?? ""),
        ("manufacturer", "Producent", a => a.Manufacturer ?? ""),
        ("model", "Model", a => a.Model ?? ""),
        ("value", "Cena zakupu", a => a.PurchasePrice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""),
        ("currency", "Waluta", a => a.Currency ?? ""),
        ("purchaseDate", "Data zakupu", a => a.PurchaseDate?.ToString("yyyy-MM-dd") ?? ""),
        ("warranty", "Gwarancja do", a => a.WarrantyUntil?.ToString("yyyy-MM-dd") ?? "")
    ];

    public async Task<Result<string>> ExportCsvAsync(string? search, AssetStatus? status, string? location, Guid? teamId, Guid? categoryId, bool unassignedOnly, bool warrantyExpiring, string? sortKey, bool sortDesc, string? columns, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var (assets, categories, people, teams) = await LoadForExportAsync(search, status, location, teamId, categoryId, unassignedOnly, warrantyExpiring, sortKey, sortDesc, cancellationToken);
        var language = _currentUser.Language;

        // Name is always written: a spreadsheet whose rows cannot be told apart is not an export. Any
        // other column the caller left out of its list is simply not emitted.
        var wanted = string.IsNullOrWhiteSpace(columns)
            ? null
            : columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Include(string key) => wanted is null || key == "name" || wanted.Contains(key);

        var header = ExportColumns.Where(c => Include(c.Key)).Select(c => c.Header).ToArray();
        var csv = new StringBuilder();
        CsvWriter.WriteRow(csv, header);

        foreach (var asset in assets)
        {
            var mapped = Map(asset, categories, people, teams, language);
            var row = ExportColumns.Where(c => Include(c.Key)).Select(c => c.Value(mapped)).ToArray();
            CsvWriter.WriteRow(csv, row);
        }

        return Result<string>.Success(csv.ToString());
    }
}

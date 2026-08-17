using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
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
    private readonly ILogger<AssetService> _logger;
    private readonly IFieldEncryptor _fieldEncryptor;

    public AssetService(IAssetRepository assets, IAssetCategoryRepository categories, IPersonRepository people, ITeamRepository teams, IActivityLogRepository activity, ISubscriptionRepository subscriptions, IOrganizationRepository organizations, IOrganizationUserRepository organizationUsers, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IQrCodeGenerator qrCodeGenerator, IAppLinkBuilder linkBuilder, IEmailSender emailSender, ILogger<AssetService> logger, IFieldEncryptor fieldEncryptor)
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
        _logger = logger;
        _fieldEncryptor = fieldEncryptor;
    }

    public async Task<Result<IReadOnlyList<AssetResponse>>> ListAsync(string? search, AssetStatus? status, string? location, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<IReadOnlyList<AssetResponse>>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assets = await _assets.ListAsync(organizationId, search, status, location, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        return Result<IReadOnlyList<AssetResponse>>.Success(assets.Select(asset => Map(asset, categories, people, teams, _currentUser.Language)).ToList());
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

        var (items, total) = await _assets.ListPagedAsync(organizationId, search, status, location, teamId, categoryId, unassignedOnly, warrantyFrom, warrantyTo, sortKey, sortDesc, page, pageSize, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        return Result<PagedResult<AssetResponse>>.Success(new PagedResult<AssetResponse>(items.Select(asset => Map(asset, categories, people, teams, _currentUser.Language)).ToList(), total, page, pageSize));
    }

    public async Task<Result<AssetGroupCountsResponse>> GetGroupCountsAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<AssetGroupCountsResponse>.Failure(access.Error!);

        var (byCategory, byStatus, byPerson) = await _assets.GetGroupCountsAsync(_currentUser.OrganizationId, cancellationToken);
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

            var asset = new Asset(organizationId, request.CategoryId, request.Name, request.AssetTag);
            asset.UpdateCore(request.Name, request.AssetTag, request.SerialNumber, request.CategoryId, request.Location, request.Manufacturer, request.Model, request.PurchasePrice, request.Currency, request.PurchaseDate, request.WarrantyUntil, request.TeamId);
            asset.SetFieldValues(EncryptSensitiveFields(category, customFieldsResult.Value!));

            // Limit sprawdzany i egzekwowany atomowo pod blokadą per-organizacja (audyt P1.11) — samo
            // "policz, porównaj, wstaw" bez blokady pozwalało dwóm równoległym requestom przejść walidację
            // zanim którykolwiek zapisze wiersz, więc organizacja mogła przekroczyć limit planu.
            var withinLimit = await _unitOfWork.ExecuteWithOrganizationLockAsync(organizationId, async ct =>
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

            asset.UpdateCore(request.Name, request.AssetTag, request.SerialNumber, request.CategoryId, request.Location, request.Manufacturer, request.Model, request.PurchasePrice, request.Currency, request.PurchaseDate, request.WarrantyUntil, request.TeamId);
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
        var scanLink = _linkBuilder.BuildAssetScanLink(organizationId, asset.Id);
        var labelLines = new List<string>();
        if (organization is null || organization.QrLabelShowName) labelLines.Add(asset.Name);
        if (organization is null || organization.QrLabelShowTag) labelLines.Add(asset.AssetTag);
        return Result<string>.Success(_qrCodeGenerator.CreateLabelledAssetQrSvg(scanLink, labelLines));
    }

    public async Task<Result<PublicAssetScanResponse>> GetPublicScanAsync(Guid organizationId, Guid assetId, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
        if (asset is null) return Result<PublicAssetScanResponse>.Failure(Error.NotFound("Aktywo nie istnieje."));
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return Result<PublicAssetScanResponse>.Failure(Error.NotFound("Organizacja nie istnieje."));
        return Result<PublicAssetScanResponse>.Success(new PublicAssetScanResponse(organization.Name));
    }

    private static readonly TimeSpan PublicIssueReportCooldown = TimeSpan.FromMinutes(10);

    public async Task<Result> ReportPublicIssueAsync(Guid organizationId, Guid assetId, ReportAssetIssueRequest request, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(organizationId, assetId, cancellationToken);
        if (asset is null) return Result.Failure(Error.NotFound("Aktywo nie istnieje."));
        if (string.IsNullOrWhiteSpace(request.Message)) return Result.Failure(Error.Validation("Treść zgłoszenia jest wymagana."));

        var reporterIp = string.IsNullOrWhiteSpace(_currentUser.IpAddress) ? "unknown" : _currentUser.IpAddress;
        var actorSubject = $"public-scan:{reporterIp}";
        var since = _clock.UtcNow - PublicIssueReportCooldown;
        var reportedRecently = await _activity.ExistsRecentAsync(organizationId, "asset", asset.Id, actorSubject, "asset.scan_reported", since, cancellationToken);
        if (reportedRecently) return Result.Failure(Error.TooManyRequests("To aktywo zostało już zgłoszone niedawno. Spróbuj ponownie później."));

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
            try
            {
                await _emailSender.SendAsync(email, subject, html, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się wysłać powiadomienia o zgłoszeniu ze skanu QR do {Email}", email);
            }
        }

        _activity.Add(new ActivityLog(organizationId, "asset.scan_reported", "asset", asset.Id, actorSubject, asset.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // Zwraca zawsze plaintext (istniejąca wartość jest odszyfrowywana) — pojedynczy punkt szyfrowania to
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

    /// <summary>Szyfruje wartości pól typu Sensitive tuż przed zapisem (audyt P1.5) — wołane po
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

        var stored = asset.FieldValues.FirstOrDefault(x => x.FieldKey == fieldKey)?.Value ?? string.Empty;
        var value = stored.Length == 0 ? stored : _fieldEncryptor.Decrypt(FieldEncryptionPurposes.AssetSensitiveField, stored);
        _activity.Add(new ActivityLog(organizationId, "asset.sensitive_field_revealed", "asset", asset.Id, _currentUser.Subject, definition.Label, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(value);
    }

    /// <summary>Eksport listy aktywów do JSON. Zawiera wyłącznie dane z <see cref="AssetResponse"/> — bez materiałów dowodowych (AssetEvidence),
    /// które są danymi własnościowymi organizacji i nie podlegają eksportowi.</summary>
    public async Task<Result<string>> ExportJsonAsync(string? search, AssetStatus? status, string? location, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assets = await _assets.ListAsync(organizationId, search, status, location, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var language = _currentUser.Language;
        var payload = assets.Select(asset => Map(asset, categories, people, teams, language)).ToList();
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return Result<string>.Success(json);
    }

    /// <summary>Eksport listy aktywów do CSV. Dane identyczne jak <see cref="ExportJsonAsync"/> (bez AssetEvidence). Status
    /// eksportowany jako nazwa enuma — brak dedykowanego helpera translacji w warstwie Application.</summary>
    public async Task<Result<string>> ExportCsvAsync(string? search, AssetStatus? status, string? location, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.AssetViewers);
        if (access.IsFailure) return Result<string>.Failure(access.Error!);

        var organizationId = _currentUser.OrganizationId;
        var assets = await _assets.ListAsync(organizationId, search, status, location, cancellationToken);
        var categories = await _categories.ListAsync(organizationId, cancellationToken);
        var people = await _people.ListAsync(organizationId, null, cancellationToken);
        var teams = await _teams.ListAsync(organizationId, cancellationToken);
        var language = _currentUser.Language;

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', new[]
        {
            "Nazwa", "Tag", "Numer seryjny", "Kategoria", "Status", "Osoba", "Lokalizacja", "Zespół",
            "Producent", "Model", "Cena zakupu", "Waluta", "Data zakupu", "Gwarancja do"
        }.Select(CsvField)));

        foreach (var asset in assets)
        {
            var mapped = Map(asset, categories, people, teams, language);
            var row = new[]
            {
                mapped.Name,
                mapped.AssetTag,
                mapped.SerialNumber ?? "",
                mapped.CategoryName ?? "",
                mapped.Status.ToString(),
                mapped.AssignedPersonName ?? "",
                mapped.Location ?? "",
                mapped.TeamName ?? "",
                mapped.Manufacturer ?? "",
                mapped.Model ?? "",
                mapped.PurchasePrice?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                mapped.Currency ?? "",
                mapped.PurchaseDate?.ToString("yyyy-MM-dd") ?? "",
                mapped.WarrantyUntil?.ToString("yyyy-MM-dd") ?? ""
            };
            csv.AppendLine(string.Join(',', row.Select(CsvField)));
        }

        return Result<string>.Success(csv.ToString());
    }

    /// <summary>Escapuje pole wg RFC4180 — cudzysłów wokół pola zawierającego przecinek, cudzysłów lub nową linię,
    /// z podwojeniem wewnętrznych cudzysłowów. Pola zaczynające się od =, +, -, @ dostają wiodący apostrof,
    /// żeby Excel/Sheets nie interpretowały danych użytkownika (nazwa aktywa, lokalizacja) jako formuły
    /// (CSV/formula injection, CWE-1236).</summary>
    private static string CsvField(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;

namespace Tenebit.Application.Assets;

public sealed class AssetCategoryService
{
    private readonly IAssetCategoryRepository _categories;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AssetCategoryService(IAssetCategoryRepository categories, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<AssetCategoryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var categories = await _categories.ListAsync(_currentUser.OrganizationId, cancellationToken);
        return categories.Select(category => Map(category, _currentUser.Language)).ToList();
    }

    public async Task<Result<AssetCategoryResponse>> CreateAsync(CreateAssetCategoryRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.AssetOperator);
        if (access.IsFailure) return Result<AssetCategoryResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            if (await _categories.NameExistsAsync(organizationId, request.Name, null, cancellationToken)) return Result<AssetCategoryResponse>.Failure(Error.Conflict("Kategoria o tej nazwie już istnieje."));
            var category = new AssetCategory(organizationId, request.Name, request.Type, request.Description, request.Icon);
            _categories.Add(category);
            _activity.Add(new ActivityLog(organizationId, "asset_category.created", "asset_category", category.Id, _currentUser.Subject, category.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AssetCategoryResponse>.Success(Map(category, _currentUser.Language));
        }
        catch (DomainException ex) { return Result<AssetCategoryResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<AssetCategoryResponse>> UpdateAsync(Guid id, UpdateAssetCategoryRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<AssetCategoryResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var category = await _categories.GetAsync(organizationId, id, cancellationToken);
            if (category is null) return Result<AssetCategoryResponse>.Failure(Error.NotFound("Kategoria nie istnieje."));
            if (await _categories.NameExistsAsync(organizationId, request.Name, id, cancellationToken)) return Result<AssetCategoryResponse>.Failure(Error.Conflict("Kategoria o tej nazwie już istnieje."));
            category.Update(request.Name, request.Type, request.Description, request.Icon);
            category.SetDepreciation(request.DepreciationMonths);
            _activity.Add(new ActivityLog(organizationId, "asset_category.updated", "asset_category", category.Id, _currentUser.Subject, category.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AssetCategoryResponse>.Success(Map(category, _currentUser.Language));
        }
        catch (DomainException ex) { return Result<AssetCategoryResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return access;
        var organizationId = _currentUser.OrganizationId;
        var category = await _categories.GetAsync(organizationId, id, cancellationToken);
        if (category is null) return Result.Failure(Error.NotFound("Kategoria nie istnieje."));
        if (await _categories.IsUsedAsync(organizationId, id, cancellationToken)) return Result.Failure(Error.Conflict("Nie można usunąć kategorii używanej przez aktywa lub zestawy."));
        _categories.Remove(category);
        _activity.Add(new ActivityLog(organizationId, "asset_category.deleted", "asset_category", category.Id, _currentUser.Subject, category.Name, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<AssetCategoryResponse>> UpdateReturnPolicyAsync(Guid id, UpdateAssetCategoryReturnPolicyRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<AssetCategoryResponse>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var category = await _categories.GetAsync(organizationId, id, cancellationToken);
            if (category is null) return Result<AssetCategoryResponse>.Failure(Error.NotFound("Kategoria nie istnieje."));
            category.UpdateReturnPolicy(request.ReturnHandlingMode, request.PostReturnDisposition, request.ReturnChecklistTemplate, request.PhotoOnIssue, request.PhotoOnReturn);
            _activity.Add(new ActivityLog(organizationId, "asset_category.return_policy_updated", "asset_category", category.Id, _currentUser.Subject, category.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<AssetCategoryResponse>.Success(Map(category, _currentUser.Language));
        }
        catch (DomainException ex) { return Result<AssetCategoryResponse>.Failure(Error.Validation(ex.Message)); }
    }

    public async Task<Result<IReadOnlyList<AssetFieldDefinitionResponse>>> SaveFieldDefinitionsAsync(Guid categoryId, IReadOnlyList<SaveAssetFieldDefinitionRequest> request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<IReadOnlyList<AssetFieldDefinitionResponse>>.Failure(access.Error!);

        try
        {
            var organizationId = _currentUser.OrganizationId;
            var category = await _categories.GetAsync(organizationId, categoryId, cancellationToken);
            if (category is null) return Result<IReadOnlyList<AssetFieldDefinitionResponse>>.Failure(Error.NotFound("Kategoria nie istnieje."));

            var keys = request.Select(x => x.Key.Trim().ToLowerInvariant()).ToList();
            if (keys.Distinct().Count() != keys.Count)
            {
                return Result<IReadOnlyList<AssetFieldDefinitionResponse>>.Failure(Error.Validation("Klucze pól własnych muszą być unikalne w obrębie kategorii."));
            }

            category.ReplaceFieldDefinitions(request.Select(x => (x.Key, x.Label, x.FieldType, x.Options, x.Required)));
            _activity.Add(new ActivityLog(organizationId, "asset_category.fields_updated", "asset_category", category.Id, _currentUser.Subject, category.Name, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<IReadOnlyList<AssetFieldDefinitionResponse>>.Success(MapFieldDefinitions(category));
        }
        catch (DomainException ex) { return Result<IReadOnlyList<AssetFieldDefinitionResponse>>.Failure(Error.Validation(ex.Message)); }
    }

    private static AssetCategoryResponse Map(AssetCategory category, string language)
    {
        var name = StarterAssetCategoryTranslations.TranslateName(category.IsSystem, language, category.Name);
        var description = StarterAssetCategoryTranslations.TranslateDescription(category.IsSystem, language, category.Name, category.Description ?? string.Empty);
        return new(category.Id, name, category.Type, description, category.Icon, category.IsSystem, MapFieldDefinitions(category), category.ReturnHandlingMode, category.PostReturnDisposition, category.ReturnChecklistTemplate, category.PhotoOnIssue, category.PhotoOnReturn, category.DepreciationMonths);
    }

    private static IReadOnlyList<AssetFieldDefinitionResponse> MapFieldDefinitions(AssetCategory category) => category.FieldDefinitions
        .OrderBy(x => x.SortOrder)
        .Select(x => new AssetFieldDefinitionResponse(x.Id, x.Key, x.Label, x.FieldType, x.OptionList, x.Required))
        .ToList();
}

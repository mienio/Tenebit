using System.ComponentModel.DataAnnotations;
using Tenebit.Domain.Assets;

namespace Tenebit.Application.Assets;

public sealed record AssetFieldDefinitionResponse(Guid Id, string Key, string Label, AssetFieldType FieldType, IReadOnlyList<string> Options, bool Required);

// AUD-007: klucze pól własnych trafiały .Trim()-owane bez walidacji długości/pustki wprost do warstwy
// Application (AssetCategoryService.ReplaceFieldDefinitions) - max length dopasowane do kolumn DB.
[ValidatedRequest]
public sealed record SaveAssetFieldDefinitionRequest(
    [property: Required, StringLength(80, MinimumLength = 1)] string Key,
    [property: Required, StringLength(120, MinimumLength = 1)] string Label,
    AssetFieldType FieldType,
    [property: StringLength(1000)] string? Options,
    bool Required);

public sealed record AssetCategoryResponse(Guid Id, string Name, AssetCategoryType Type, string? Description, string? Icon, bool IsSystem, IReadOnlyList<AssetFieldDefinitionResponse> FieldDefinitions, ReturnHandlingMode ReturnHandlingMode, PostReturnDisposition PostReturnDisposition, string? ReturnChecklistTemplate, PhotoRequirement PhotoOnIssue, PhotoRequirement PhotoOnReturn, int? DepreciationMonths);
[ValidatedRequest]
public sealed record CreateAssetCategoryRequest([property: Required, StringLength(120, MinimumLength = 1)] string Name, AssetCategoryType Type, [property: StringLength(600)] string? Description, [property: StringLength(40)] string? Icon);
[ValidatedRequest]
public sealed record UpdateAssetCategoryRequest([property: Required, StringLength(120, MinimumLength = 1)] string Name, AssetCategoryType Type, [property: StringLength(600)] string? Description, [property: StringLength(40)] string? Icon, [property: Range(1, 1200)] int? DepreciationMonths = null);
[ValidatedRequest]
public sealed record UpdateAssetCategoryReturnPolicyRequest(ReturnHandlingMode ReturnHandlingMode, PostReturnDisposition PostReturnDisposition, string? ReturnChecklistTemplate, PhotoRequirement PhotoOnIssue, PhotoRequirement PhotoOnReturn);

/// <summary>Book value of the whole fleet, and per category, under each category's depreciation schedule.</summary>
public sealed record FleetValueResponse(
    decimal TotalPurchaseValue,
    decimal TotalCurrentValue,
    decimal TotalDepreciated,
    int AssetsWithValue,
    int AssetsWithoutPrice,
    string Currency,
    IReadOnlyList<CategoryValueSlice> ByCategory);

/// <summary>Running totals while grouping assets by category; internal to the fleet-value calculation.</summary>
internal sealed record CategoryAccumulator(string Name, int? Months, int Count, decimal Purchase, decimal Current);

public sealed record CategoryValueSlice(
    Guid CategoryId,
    string CategoryName,
    int? DepreciationMonths,
    int AssetCount,
    decimal PurchaseValue,
    decimal CurrentValue);

public sealed record AssetResponse(
    Guid Id,
    string Name,
    string AssetTag,
    string? SerialNumber,
    Guid CategoryId,
    string? CategoryName,
    AssetStatus Status,
    Guid? AssignedPersonId,
    string? AssignedPersonName,
    string? Location,
    string? Manufacturer,
    string? Model,
    decimal? PurchasePrice,
    string? Currency,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    string QrCodePayload,
    DateTimeOffset UpdatedAt,
    IReadOnlyDictionary<string, string> CustomFields,
    IReadOnlyList<AssetFieldDefinitionResponse> CategoryFieldDefinitions,
    Guid? TeamId,
    string? TeamName);

[ValidatedRequest]
public sealed record CreateAssetRequest(
    string Name,
    string AssetTag,
    string? SerialNumber,
    Guid CategoryId,
    string? Location,
    string? Manufacturer,
    string? Model,
    decimal? PurchasePrice,
    string? Currency,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    Guid? TeamId,
    IReadOnlyDictionary<string, string>? CustomFields);

public sealed record AssetGroupCountsResponse(
    IReadOnlyDictionary<Guid, int> ByCategory,
    IReadOnlyDictionary<AssetStatus, int> ByStatus,
    IReadOnlyDictionary<Guid, int> ByPerson);

public sealed record PublicAssetScanResponse(string OrganizationName);
[ValidatedRequest]
public sealed record ReportAssetIssueRequest(string Message);

[ValidatedRequest]
public sealed record UpdateAssetRequest(
    string Name,
    string AssetTag,
    string? SerialNumber,
    Guid CategoryId,
    AssetStatus Status,
    string? Location,
    string? Manufacturer,
    string? Model,
    decimal? PurchasePrice,
    string? Currency,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    Guid? TeamId,
    IReadOnlyDictionary<string, string>? CustomFields);

using Tenebit.Domain.Assets;

namespace Tenebit.Application.Assets;

public sealed record AssetFieldDefinitionResponse(Guid Id, string Key, string Label, AssetFieldType FieldType, IReadOnlyList<string> Options, bool Required);
public sealed record SaveAssetFieldDefinitionRequest(string Key, string Label, AssetFieldType FieldType, string? Options, bool Required);

public sealed record AssetCategoryResponse(Guid Id, string Name, AssetCategoryType Type, string? Description, string? Icon, bool IsSystem, IReadOnlyList<AssetFieldDefinitionResponse> FieldDefinitions, ReturnHandlingMode ReturnHandlingMode, PostReturnDisposition PostReturnDisposition, string? ReturnChecklistTemplate, PhotoRequirement PhotoOnIssue, PhotoRequirement PhotoOnReturn);
public sealed record CreateAssetCategoryRequest(string Name, AssetCategoryType Type, string? Description, string? Icon);
public sealed record UpdateAssetCategoryRequest(string Name, AssetCategoryType Type, string? Description, string? Icon);
public sealed record UpdateAssetCategoryReturnPolicyRequest(ReturnHandlingMode ReturnHandlingMode, PostReturnDisposition PostReturnDisposition, string? ReturnChecklistTemplate, PhotoRequirement PhotoOnIssue, PhotoRequirement PhotoOnReturn);

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

public sealed record PublicAssetScanResponse(string OrganizationName);
public sealed record ReportAssetIssueRequest(string Message);

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

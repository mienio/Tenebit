namespace Tenebit.Application.Settings;

public sealed record AssetStatusSettingResponse(string StatusKey, string Label, int SortOrder, bool IsEnabled);
public sealed record SaveAssetStatusSettingRequest(string StatusKey, string Label, int SortOrder, bool IsEnabled);

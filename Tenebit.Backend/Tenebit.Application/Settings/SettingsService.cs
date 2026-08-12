using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Common;
using Tenebit.Domain.Settings;

namespace Tenebit.Application.Settings;

public sealed class SettingsService
{
    private readonly IAssetStatusSettingRepository _statusSettings;
    private readonly IActivityLogRepository _activity;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SettingsService(IAssetStatusSettingRepository statusSettings, IActivityLogRepository activity, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork)
    {
        _statusSettings = statusSettings;
        _activity = activity;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<AssetStatusSettingResponse>> ListAssetStatusesAsync(CancellationToken cancellationToken)
    {
        var saved = await _statusSettings.ListAsync(_currentUser.OrganizationId, cancellationToken);
        if (saved.Count == 0)
        {
            return BuiltInStatusSettings(_currentUser.Language);
        }

        return saved.OrderBy(x => x.SortOrder).Select(x => new AssetStatusSettingResponse(x.StatusKey, x.Label.Trim(), x.Color, x.BackgroundColor, x.SortOrder, x.IsEnabled)).ToList();
    }

    public async Task<Result<IReadOnlyList<AssetStatusSettingResponse>>> SaveAssetStatusesAsync(IReadOnlyList<SaveAssetStatusSettingRequest> request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<IReadOnlyList<AssetStatusSettingResponse>>.Failure(access.Error!);
        try
        {
            var organizationId = _currentUser.OrganizationId;
            var allowed = Enum.GetNames<AssetStatus>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in request)
            {
                if (!allowed.Contains(item.StatusKey)) return Result<IReadOnlyList<AssetStatusSettingResponse>>.Failure(Error.Validation($"Nieznany status aktywa: {item.StatusKey}."));
                var setting = await _statusSettings.GetByKeyAsync(organizationId, item.StatusKey, cancellationToken);
                if (setting is null)
                {
                    setting = new AssetStatusSetting(organizationId, item.StatusKey, item.Label, item.Color, item.BackgroundColor, item.SortOrder, item.IsEnabled);
                    _statusSettings.Add(setting);
                }
                else
                {
                    setting.Update(item.Label, item.Color, item.BackgroundColor, item.SortOrder, item.IsEnabled);
                }
            }
            _activity.Add(new ActivityLog(organizationId, "settings.asset_statuses.updated", "settings", organizationId, _currentUser.Subject, null, _clock.UtcNow));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var statuses = await ListAssetStatusesAsync(cancellationToken);
            return Result<IReadOnlyList<AssetStatusSettingResponse>>.Success(statuses);
        }
        catch (DomainException ex) { return Result<IReadOnlyList<AssetStatusSettingResponse>>.Failure(Error.Validation(ex.Message)); }
    }

    private static IReadOnlyList<AssetStatusSettingResponse> BuiltInStatusSettings(string language)
    {
        if (language != "pl")
        {
            return
            [
                new(nameof(AssetStatus.Draft), "Draft", "#475569", "#f8fafc", 10, true),
                new(nameof(AssetStatus.InStock), "In stock", "#047857", "#ecfdf5", 20, true),
                new(nameof(AssetStatus.Reserved), "Reserved", "#1d4ed8", "#eff6ff", 30, true),
                new(nameof(AssetStatus.Assigned), "Assigned", "#1d4ed8", "#eff6ff", 40, true),
                new(nameof(AssetStatus.InTransit), "In transit", "#c2410c", "#fff7ed", 50, true),
                new(nameof(AssetStatus.InService), "In service", "#c2410c", "#fff7ed", 60, true),
                new(nameof(AssetStatus.Damaged), "Damaged", "#be123c", "#fff1f2", 70, true),
                new(nameof(AssetStatus.Lost), "Lost", "#be123c", "#fff1f2", 80, true),
                new(nameof(AssetStatus.Retired), "Retired", "#475569", "#f8fafc", 90, true),
                new(nameof(AssetStatus.Disposed), "Disposed", "#991b1b", "#fef2f2", 100, true)
            ];
        }

        return
        [
            new(nameof(AssetStatus.Draft), "Szkic", "#475569", "#f8fafc", 10, true),
            new(nameof(AssetStatus.InStock), "W magazynie", "#047857", "#ecfdf5", 20, true),
            new(nameof(AssetStatus.Reserved), "Zarezerwowane", "#1d4ed8", "#eff6ff", 30, true),
            new(nameof(AssetStatus.Assigned), "Wydane", "#1d4ed8", "#eff6ff", 40, true),
            new(nameof(AssetStatus.InTransit), "W drodze", "#c2410c", "#fff7ed", 50, true),
            new(nameof(AssetStatus.InService), "W serwisie", "#c2410c", "#fff7ed", 60, true),
            new(nameof(AssetStatus.Damaged), "Uszkodzone", "#be123c", "#fff1f2", 70, true),
            new(nameof(AssetStatus.Lost), "Zaginione", "#be123c", "#fff1f2", 80, true),
            new(nameof(AssetStatus.Retired), "Wycofane", "#475569", "#f8fafc", 90, true),
            new(nameof(AssetStatus.Disposed), "Zutylizowane", "#991b1b", "#fef2f2", 100, true)
        ];
    }
}

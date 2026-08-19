using System.Net.Mail;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;

namespace Tenebit.Application.Alerts;

public sealed class AlertSettingsService
{
    private readonly IAlertRuleRepository _rules;
    private readonly IAlertDigestSettingsRepository _digestSettings;
    private readonly ISentAlertRepository _sentAlerts;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AlertSettingsService(
        IAlertRuleRepository rules,
        IAlertDigestSettingsRepository digestSettings,
        ISentAlertRepository sentAlerts,
        IEmailSender emailSender,
        IClock clock,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _rules = rules;
        _digestSettings = digestSettings;
        _sentAlerts = sentAlerts;
        _emailSender = emailSender;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<AlertRuleResponse>>> ListAlertRulesAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<IReadOnlyList<AlertRuleResponse>>.Failure(access.Error!);

        var rules = await _rules.ListByOrganizationAsync(_currentUser.OrganizationId, cancellationToken);

        var allTypes = Enum.GetValues<AlertType>();
        var dict = rules.ToDictionary(r => r.Type);

        var responses = allTypes.Select(type => dict.TryGetValue(type, out var rule)
            ? MapRule(rule)
            : DefaultRuleResponse(type))
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<AlertRuleResponse>>.Success(responses);
    }

    public async Task<Result<AlertRuleResponse>> GetAlertRuleAsync(AlertType type, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<AlertRuleResponse>.Failure(access.Error!);

        var rule = await _rules.GetAsync(_currentUser.OrganizationId, type, cancellationToken);
        if (rule is null) return Result<AlertRuleResponse>.Success(DefaultRuleResponse(type));

        return Result<AlertRuleResponse>.Success(MapRule(rule));
    }

    private static AlertRuleResponse DefaultRuleResponse(AlertType type) =>
        new(type, false, new(), AlertDeliveryMode.Immediate, AlertRecipientMode.OwnersAndAdmins, null, 1);

    public async Task<Result<AlertRuleResponse>> UpsertAlertRuleAsync(AlertType type, SaveAlertRuleRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<AlertRuleResponse>.Failure(access.Error!);

        if (request.ThresholdDays.Count > AlertRule.MaxThresholdCount)
            return Result<AlertRuleResponse>.Failure(Error.Validation($"Maksymalna liczba progów to {AlertRule.MaxThresholdCount}."));

        foreach (var d in request.ThresholdDays)
        {
            if (d < 0 || d > AlertRule.MaxThresholdDays)
                return Result<AlertRuleResponse>.Failure(Error.Validation($"Próg musi być w zakresie 0–{AlertRule.MaxThresholdDays} dni."));
        }

        if (request.RecipientMode == AlertRecipientMode.Custom)
        {
            var invalid = ValidateCustomEmails(request.CustomEmails);
            if (invalid is not null)
                return Result<AlertRuleResponse>.Failure(Error.Validation(invalid));
        }

        if (request.CooldownDays is < 0 or > 14)
            return Result<AlertRuleResponse>.Failure(Error.Validation("Cooldown musi być w zakresie 0–14 dni."));

        var now = _clock.UtcNow;
        var rule = await _rules.GetAsync(_currentUser.OrganizationId, type, cancellationToken);

        if (rule is null)
        {
            rule = new AlertRule(_currentUser.OrganizationId, type, now, _currentUser.Email);
            _rules.Add(rule);
        }

        rule.UpdateSettings(
            request.IsEnabled,
            request.ThresholdDays,
            request.DeliveryMode,
            request.RecipientMode,
            request.CustomEmails,
            request.CooldownDays,
            _currentUser.Email,
            now);
        _rules.Update(rule);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AlertRuleResponse>.Success(MapRule(rule));
    }

    public async Task<Result<AlertDigestSettingsResponse>> GetAlertDigestAsync(CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<AlertDigestSettingsResponse>.Failure(access.Error!);

        var settings = await _digestSettings.GetAsync(_currentUser.OrganizationId, cancellationToken);
        if (settings is null) settings = new AlertDigestSettings(_currentUser.OrganizationId);

        return Result<AlertDigestSettingsResponse>.Success(MapDigest(settings));
    }

    public async Task<Result<AlertDigestSettingsResponse>> UpsertAlertDigestAsync(SaveAlertDigestSettingsRequest request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result<AlertDigestSettingsResponse>.Failure(access.Error!);

        if (request.Frequency == AlertDigestFrequency.Weekly && request.DayOfWeek is null)
            return Result<AlertDigestSettingsResponse>.Failure(Error.Validation("Dla digestu tygodniowego konieczny jest dzień tygodnia."));

        if (request.QuietHoursStart.HasValue != request.QuietHoursEnd.HasValue)
            return Result<AlertDigestSettingsResponse>.Failure(Error.Validation("Godziny cichego trybu muszą być ustawione razem."));

        var orgId = _currentUser.OrganizationId;
        var settings = await _digestSettings.GetAsync(orgId, cancellationToken);
        var now = _clock.UtcNow;

        if (settings is null)
        {
            settings = new AlertDigestSettings(orgId);
            settings.Update(
                request.Frequency, request.DayOfWeek, request.LocalTime,
                request.QuietHoursStart, request.QuietHoursEnd,
                request.BusinessDays, request.HolidayCalendarCountryCode,
                request.IncludeEmptyDigest, null);
            _digestSettings.Add(settings);
        }
        else
        {
            settings.Update(
                request.Frequency, request.DayOfWeek, request.LocalTime,
                request.QuietHoursStart, request.QuietHoursEnd,
                request.BusinessDays, request.HolidayCalendarCountryCode,
                request.IncludeEmptyDigest, settings.LastGeneratedAt);
            _digestSettings.Update(settings);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AlertDigestSettingsResponse>.Success(MapDigest(settings));
    }

    public async Task<Result> SendTestAlertAsync(AlertTestRequest? request, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin);
        if (access.IsFailure) return Result.Failure(access.Error!);

        if (string.IsNullOrWhiteSpace(_currentUser.Email))
            return Result.Failure(Error.Validation("Brak adresu e-mail dla zalogowanego użytkownika."));

        var alertType = request?.AlertType ?? AlertType.AssetWarrantyExpiring;
        var subject = $"Tenebit - test alert: {alertType}";
        var body = BuildTestEmailBody(alertType, _currentUser.Email, _currentUser.OrganizationId);

        await _emailSender.SendAsync(_currentUser.Email, subject, body, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PagedResult<SentAlertHistoryItemResponse>>> ListSentAlertHistoryAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = AccessPolicy.EnsureAnyRole(_currentUser, TenebitRoles.Owner, TenebitRoles.Admin, TenebitRoles.Auditor);
        if (access.IsFailure) return Result<PagedResult<SentAlertHistoryItemResponse>>.Failure(access.Error!);

        var (items, total) = await _sentAlerts.ListPagedAsync(_currentUser.OrganizationId, page, pageSize, cancellationToken);

        var responses = items.Select(MapHistoryItem).ToList().AsReadOnly();
        return Result<PagedResult<SentAlertHistoryItemResponse>>.Success(
            new PagedResult<SentAlertHistoryItemResponse>(responses, total, page, pageSize));
    }

    private static AlertRuleResponse MapRule(AlertRule rule) =>
        new(rule.Type, rule.IsEnabled, rule.ThresholdDays, rule.DeliveryMode, rule.RecipientMode, rule.CustomEmails, rule.CooldownDays);

    private static AlertDigestSettingsResponse MapDigest(AlertDigestSettings s) =>
        new(s.Frequency, s.DayOfWeek, s.LocalTime, s.QuietHoursStart, s.QuietHoursEnd, s.BusinessDays, s.HolidayCalendarCountryCode, s.IncludeEmptyDigest);

    private static SentAlertHistoryItemResponse MapHistoryItem(SentAlert alert) =>
        new(
            alert.Id,
            ParseAlertType(alert.AlertKey),
            alert.EntityId,
            alert.RecipientEmail,
            alert.Status,
            alert.CreatedAt,
            alert.SentAt,
            alert.LastError);

    private static AlertType ParseAlertType(string alertKey)
    {
        var firstSegment = alertKey.Split(':')[0];
        return Enum.TryParse<AlertType>(firstSegment, ignoreCase: true, out var t) ? t : AlertType.AssetWarrantyExpiring;
    }

    private static string? ValidateCustomEmails(string? customEmails)
    {
        if (string.IsNullOrWhiteSpace(customEmails)) return null;

        foreach (var part in customEmails.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!MailAddress.TryCreate(part, out _))
                return $"Nieprawidłowy adres e-mail: {part}.";
        }

        return null;
    }

    private static string BuildTestEmailBody(AlertType alertType, string recipient, Guid organizationId) =>
        $"""
        <html>
        <head><meta charset="utf-8" /><title>Test alert</title></head>
        <body style="font-family: sans-serif; color: #1f2937;">
        <h2 style="color: #2563eb;">Tenebit - test alert</h2>
        <p>Otrzymałeś tego maila jako <strong>{recipient}</strong>.</p>
        <p>Jest to wiadomość testowa. Konfiguracja alertów działa poprawnie.</p>
        <table style="border-collapse: collapse; margin-top: 16px;">
        <tr><td style="padding: 4px 8px; border-bottom: 1px solid #e5e7eb;"><strong>Typ alertu:</strong></td><td style="padding: 4px 8px; border-bottom: 1px solid #e5e7eb;">{alertType}</td></tr>
        <tr><td style="padding: 4px 8px; border-bottom: 1px solid #e5e7eb;"><strong>Organizacja:</strong></td><td style="padding: 4px 8px; border-bottom: 1px solid #e5e7eb;">{organizationId}</td></tr>
        <tr><td style="padding: 4px 8px;"><strong>Data:</strong></td><td style="padding: 4px 8px;">{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</td></tr>
        </table>
        </body>
        </html>
        """;
}

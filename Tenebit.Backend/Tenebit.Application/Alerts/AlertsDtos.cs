using Tenebit.Domain.Alerts;

namespace Tenebit.Application.Alerts;

public sealed record AlertRuleResponse(
    AlertType Type,
    bool IsEnabled,
    List<int> ThresholdDays,
    AlertDeliveryMode DeliveryMode,
    AlertRecipientMode RecipientMode,
    string? CustomEmails,
    int CooldownDays);

[ValidatedRequest]
public sealed record SaveAlertRuleRequest(
    bool IsEnabled,
    List<int> ThresholdDays,
    AlertDeliveryMode DeliveryMode,
    AlertRecipientMode RecipientMode,
    string? CustomEmails,
    int CooldownDays);

public sealed record AlertDigestSettingsResponse(
    AlertDigestFrequency Frequency,
    DayOfWeek? DayOfWeek,
    TimeOnly LocalTime,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    AlertDigestBusinessDays BusinessDays,
    string? HolidayCalendarCountryCode,
    bool IncludeEmptyDigest);

[ValidatedRequest]
public sealed record SaveAlertDigestSettingsRequest(
    AlertDigestFrequency Frequency,
    DayOfWeek? DayOfWeek,
    TimeOnly LocalTime,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    AlertDigestBusinessDays BusinessDays,
    string? HolidayCalendarCountryCode,
    bool IncludeEmptyDigest);

[ValidatedRequest]
public sealed record AlertTestRequest(AlertType? AlertType);

public sealed record SentAlertHistoryItemResponse(
    Guid Id,
    AlertType Type,
    Guid EntityId,
    string RecipientEmail,
    SentAlertStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? LastError);

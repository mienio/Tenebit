using Tenebit.Domain.Common;

namespace Tenebit.Domain.Alerts;

public sealed class AlertDigestSettings
{
    private AlertDigestSettings() { }

    public AlertDigestSettings(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new DomainException("Identyfikator organizacji jest wymagany.");

        OrganizationId = organizationId;
        Frequency = AlertDigestFrequency.Off;
        LocalTime = new TimeOnly(8, 0);
        BusinessDays = AlertDigestBusinessDays.Weekdays;
        IncludeEmptyDigest = false;
    }

    public Guid OrganizationId { get; private set; }
    public AlertDigestFrequency Frequency { get; private set; }
    public DayOfWeek? DayOfWeek { get; private set; }
    public TimeOnly LocalTime { get; private set; }
    public TimeOnly? QuietHoursStart { get; private set; }
    public TimeOnly? QuietHoursEnd { get; private set; }
    public AlertDigestBusinessDays BusinessDays { get; private set; }
    public string? HolidayCalendarCountryCode { get; private set; }
    public bool IncludeEmptyDigest { get; private set; }
    public DateTimeOffset? LastGeneratedAt { get; private set; }

    public void Update(
        AlertDigestFrequency frequency,
        DayOfWeek? dayOfWeek,
        TimeOnly localTime,
        TimeOnly? quietHoursStart,
        TimeOnly? quietHoursEnd,
        AlertDigestBusinessDays businessDays,
        string? holidayCalendarCountryCode,
        bool includeEmptyDigest,
        DateTimeOffset? lastGeneratedAt)
    {
        Frequency = frequency;
        DayOfWeek = dayOfWeek;
        LocalTime = localTime;
        QuietHoursStart = quietHoursStart;
        QuietHoursEnd = quietHoursEnd;
        BusinessDays = businessDays;
        HolidayCalendarCountryCode = holidayCalendarCountryCode;
        IncludeEmptyDigest = includeEmptyDigest;
        LastGeneratedAt = lastGeneratedAt;
    }
}

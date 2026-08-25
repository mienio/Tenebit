using Tenebit.Domain.Common;

namespace Tenebit.Domain.Assets;

/// <summary>
/// A recurring obligation attached to an asset: a fire-extinguisher inspection every 12 months, a
/// ladder check every 6, an electrical measurement every 24. The product already tracked one-off dates
/// (warranty, licence expiry) but had no concept of something that comes back, which is exactly the
/// category of deadline companies are legally required to keep.
///
/// The schedule stores its own next due date rather than deriving it from the last completion, because
/// a real inspection often happens early or late and the next one is then counted from when it actually
/// took place - not from when it was supposed to.
/// </summary>
public sealed class MaintenanceSchedule
{
    private MaintenanceSchedule() { }

    public MaintenanceSchedule(
        Guid organizationId,
        Guid assetId,
        string name,
        int intervalMonths,
        DateOnly nextDueOn,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        AssetId = assetId;
        CreatedAt = createdAt;
        IsActive = true;
        Rename(name);
        SetInterval(intervalMonths);
        NextDueOn = nextDueOn;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AssetId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>Months between occurrences. Kept in months, not days, because these obligations are
    /// always expressed that way ("annual inspection"), and months keep the date on the same day.</summary>
    public int IntervalMonths { get; private set; }

    public DateOnly NextDueOn { get; private set; }
    public DateOnly? LastPerformedOn { get; private set; }
    public string? LastPerformedBy { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Nazwa przeglądu jest wymagana.");
        }

        Name = name.Trim()[..Math.Min(name.Trim().Length, 160)];
    }

    public void SetInterval(int intervalMonths)
    {
        if (intervalMonths is < 1 or > 120)
        {
            throw new DomainException("Częstotliwość przeglądu musi mieścić się w zakresie 1-120 miesięcy.");
        }

        IntervalMonths = intervalMonths;
    }

    public void Reschedule(DateOnly nextDueOn) => NextDueOn = nextDueOn;

    /// <summary>
    /// Records a completed inspection and moves the deadline forward from the date it was actually done.
    /// </summary>
    public void MarkPerformed(DateOnly performedOn, string? performedBy)
    {
        LastPerformedOn = performedOn;
        LastPerformedBy = string.IsNullOrWhiteSpace(performedBy) ? null : performedBy.Trim()[..Math.Min(performedBy.Trim().Length, 240)];
        NextDueOn = performedOn.AddMonths(IntervalMonths);
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>Negative when the deadline has passed - the UI colours and sorts on this.</summary>
    public int DaysRemaining(DateOnly today) => NextDueOn.DayNumber - today.DayNumber;
}

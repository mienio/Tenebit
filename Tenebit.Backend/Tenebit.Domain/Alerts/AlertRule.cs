using Tenebit.Domain.Common;

namespace Tenebit.Domain.Alerts;

public sealed class AlertRule
{
    public const int MaxThresholdDays = 365;
    public const int MaxThresholdCount = 5;

    private AlertRule() { }

    public AlertRule(Guid organizationId, AlertType type, DateTimeOffset createdAt, string createdBy)
    {
        if (organizationId == Guid.Empty) throw new DomainException("Identyfikator organizacji jest wymagany.");
        if (string.IsNullOrWhiteSpace(createdBy)) throw new DomainException("Użytkownik tworzący jest wymagany.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Type = type;
        IsEnabled = true;
        ThresholdDays = new List<int>();
        DeliveryMode = AlertDeliveryMode.Immediate;
        RecipientMode = AlertRecipientMode.OwnersAndAdmins;
        CooldownDays = 1;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        UpdatedBy = createdBy;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public AlertType Type { get; private set; }
    public bool IsEnabled { get; private set; }
    public List<int> ThresholdDays { get; private set; } = new();
    public AlertDeliveryMode DeliveryMode { get; private set; }
    public AlertRecipientMode RecipientMode { get; private set; }
    public string? CustomEmails { get; private set; }
    public int CooldownDays { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    public void UpdateSettings(
        bool isEnabled,
        List<int> thresholdDays,
        AlertDeliveryMode deliveryMode,
        AlertRecipientMode recipientMode,
        string? customEmails,
        int cooldownDays,
        string updatedBy,
        DateTimeOffset updatedAt)
    {
        IsEnabled = isEnabled;
        ThresholdDays = thresholdDays;
        DeliveryMode = deliveryMode;
        RecipientMode = recipientMode;
        CustomEmails = customEmails;
        CooldownDays = cooldownDays;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    public void Enable(DateTimeOffset updatedAt, string updatedBy)
    {
        IsEnabled = true;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public void Disable(DateTimeOffset updatedAt, string updatedBy)
    {
        IsEnabled = false;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }
}

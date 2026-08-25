using Tenebit.Domain.Alerts;

namespace Tenebit.Application.Alerts;

/// <summary>
/// Domyślne reguły alertów dla nowo tworzonej organizacji. Trzy typy, które przed wprowadzeniem
/// konfigurowalnych alertów działały bezwarunkowo (gwarancja, termin zwrotu wydania, brak potwierdzenia
/// wydania), są domyślnie włączone - żeby wdrożenie nie uciszyło istniejących powiadomień. Pozostałe
/// (nowe kategorie) startują wyłączone i administrator włącza je ręcznie w zakładce Alerty.
/// </summary>
public static class StarterAlertRules
{
    private static readonly (AlertType Type, bool IsEnabled, int[] Thresholds)[] Defaults =
    [
        (AlertType.AssetWarrantyExpiring, true, [30, 7]),
        (AlertType.AssignmentReturnDue, true, [0]),
        (AlertType.AssignmentNotConfirmed, true, [0]),
        (AlertType.LicenseExpiring, false, [30, 7]),
        (AlertType.ProcedureReviewDue, false, [30, 7]),
        (AlertType.OffboardingReturnDue, false, [7]),
        (AlertType.AssetAuditNoResponse, false, [7]),
        (AlertType.ReservationAwaitingApproval, false, [1]),
        (AlertType.ReservationPickupUpcoming, false, [1]),
        (AlertType.ReservationOverdue, false, [0]),
        (AlertType.MaintenanceDue, true, [30, 7])
    ];

    public static IReadOnlyList<AlertRule> Create(Guid organizationId, DateTimeOffset createdAt, string createdBy)
    {
        var rules = new List<AlertRule>(Defaults.Length);
        foreach (var (type, isEnabled, thresholds) in Defaults)
        {
            var rule = new AlertRule(organizationId, type, createdAt, createdBy);
            rule.UpdateSettings(isEnabled, thresholds.ToList(), AlertDeliveryMode.Immediate, AlertRecipientMode.OwnersAndAdmins, null, 1, createdBy, createdAt);
            rules.Add(rule);
        }
        return rules;
    }
}

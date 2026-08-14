using System.Net;
using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Reservations;

namespace Tenebit.Application.Alerts;

public sealed class AlertCheckService
{
    private const int MaxSendAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationUserRepository _users;
    private readonly IAssetRepository _assets;
    private readonly IAssignmentRepository _assignments;
    private readonly IProcedureRepository _procedures;
    private readonly IPersonRepository _people;
    private readonly ISentAlertRepository _sentAlerts;
    private readonly IAlertRuleRepository _rules;
    private readonly IAlertDigestSettingsRepository _digestSettings;
    private readonly IOffboardingCaseRepository _offboarding;
    private readonly IAssetAuditCampaignRepository _auditCampaigns;
    private readonly IAssetAuditParticipantRepository _auditParticipants;
    private readonly IEquipmentReservationRepository _reservations;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AlertCheckService(
        IOrganizationRepository organizations,
        IOrganizationUserRepository users,
        IAssetRepository assets,
        IAssignmentRepository assignments,
        IProcedureRepository procedures,
        IPersonRepository people,
        ISentAlertRepository sentAlerts,
        IAlertRuleRepository rules,
        IAlertDigestSettingsRepository digestSettings,
        IOffboardingCaseRepository offboarding,
        IAssetAuditCampaignRepository auditCampaigns,
        IAssetAuditParticipantRepository auditParticipants,
        IEquipmentReservationRepository reservations,
        IEmailSender emailSender,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _users = users;
        _assets = assets;
        _assignments = assignments;
        _procedures = procedures;
        _people = people;
        _sentAlerts = sentAlerts;
        _rules = rules;
        _digestSettings = digestSettings;
        _offboarding = offboarding;
        _auditCampaigns = auditCampaigns;
        _auditParticipants = auditParticipants;
        _reservations = reservations;
        _emailSender = emailSender;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    // onboardingDeadlineDays is read from configuration by the caller (AlertBackgroundService) — kept as a
    // plain parameter here so the Application layer doesn't need a dependency on Microsoft.Extensions.Configuration.
    public async Task RunAsync(int onboardingDeadlineDays, CancellationToken cancellationToken)
    {
        var organizations = await _organizations.ListAllAsync(cancellationToken);
        foreach (var organization in organizations)
        {
            var rules = await _rules.ListByOrganizationAsync(organization.Id, cancellationToken);
            if (rules.Count == 0) continue;

            var isQuietHours = organization.IsWithinQuietHours(_clock.UtcNow);
            var digestItems = new List<DigestItem>();
            var hasChanges = false;

            hasChanges |= await CheckWarrantyAlertsAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await CheckAssignmentReturnDueAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await CheckAssignmentNotConfirmedAsync(organization, onboardingDeadlineDays, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await CheckOffboardingReturnDueAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await CheckAssetAuditNoResponseAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await CheckReservationAwaitingApprovalAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await CheckReservationPickupUpcomingAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await CheckReservationOverdueAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
            hasChanges |= await TryGenerateDigestAsync(organization, digestItems, isQuietHours, cancellationToken);

            if (hasChanges)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }

    // ---------- detection methods ----------

    private async Task<bool> CheckWarrantyAlertsAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.AssetWarrantyExpiring);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var assets = await _assets.ListAsync(organization.Id, null, null, null, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in NormalizeThresholds(rule))
        {
            var targetDate = today.AddDays(thresholdDays);
            foreach (var asset in assets.Where(a => a.WarrantyUntil.HasValue && a.WarrantyUntil.Value >= today && a.WarrantyUntil.Value <= targetDate))
            {
                var subject = $"Gwarancja wygasa za {thresholdDays} dni — {asset.Name} ({asset.AssetTag})";
                var html = $"<p>Gwarancja na aktywo <strong>{Encode(asset.Name)}</strong> (tag: {Encode(asset.AssetTag)}) wygasa <strong>{asset.WarrantyUntil:yyyy-MM-dd}</strong> ({thresholdDays} dni).</p>";
                events.Add(new AlertEvent(asset.Id, thresholdDays, asset.WarrantyUntil, subject, html, []));
            }
        }

        return await EmitAsync(organization, rule, AlertType.AssetWarrantyExpiring, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckAssignmentReturnDueAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.AssignmentReturnDue);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var assignments = await _assignments.ListAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in NormalizeThresholds(rule))
        {
            var targetDate = today.AddDays(thresholdDays);
            foreach (var assignment in assignments.Where(a => a.DueDate.HasValue && a.DueDate.Value <= targetDate && a.Status is AssignmentStatus.AwaitingAcceptance or AssignmentStatus.Accepted))
            {
                var person = await _people.GetAsync(organization.Id, assignment.PersonId, cancellationToken);
                var subject = $"Termin zwrotu wydania — protokół {assignment.ProtocolNumber}";
                var html = $"<p>Termin zwrotu sprzętu z protokołu <strong>{Encode(assignment.ProtocolNumber)}</strong> dla osoby <strong>{Encode(person?.FullName)}</strong> to <strong>{assignment.DueDate:yyyy-MM-dd}</strong>.</p>";
                events.Add(new AlertEvent(assignment.Id, thresholdDays, assignment.DueDate, subject, html, PersonEmail(person)));
            }
        }

        return await EmitAsync(organization, rule, AlertType.AssignmentReturnDue, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckAssignmentNotConfirmedAsync(Organization organization, int deadlineDays, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.AssignmentNotConfirmed);
        if (rule is null) return false;

        deadlineDays = Math.Max(1, deadlineDays);
        var deadline = _clock.UtcNow.AddDays(-deadlineDays);
        var assignments = await _assignments.ListAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var assignment in assignments.Where(a => a.Status == AssignmentStatus.AwaitingAcceptance && a.IssuedAt <= deadline))
        {
            var person = await _people.GetAsync(organization.Id, assignment.PersonId, cancellationToken);
            var subject = $"Sprzęt nieodebrany — protokół {assignment.ProtocolNumber}";
            var html = $"<p>Sprzęt z protokołu <strong>{Encode(assignment.ProtocolNumber)}</strong> dla osoby <strong>{Encode(person?.FullName)}</strong> nie został odebrany w ciągu {deadlineDays} dni od wysłania.</p>";
            events.Add(new AlertEvent(assignment.Id, deadlineDays, DateOnly.FromDateTime(assignment.IssuedAt.UtcDateTime), subject, html, PersonEmail(person)));
        }

        var pendingAcceptances = assignments
            .SelectMany(a => a.ProcedureAcceptances.Where(p => p.Status == AcceptanceStatus.Pending && p.SentAt <= deadline).Select(p => (Assignment: a, Acceptance: p)))
            .ToList();

        if (pendingAcceptances.Count > 0)
        {
            var procedureIds = pendingAcceptances.Select(x => x.Acceptance.ProcedureId).Distinct().ToArray();
            var procedures = await _procedures.GetByIdsAsync(organization.Id, procedureIds, cancellationToken);

            foreach (var (assignment, acceptance) in pendingAcceptances)
            {
                var person = await _people.GetAsync(organization.Id, acceptance.PersonId, cancellationToken);
                var procedure = procedures.FirstOrDefault(x => x.Id == acceptance.ProcedureId);
                var (subject, html) = EmailTemplates.ProcedureUnsignedAlert(organization.Language, procedure?.Title, assignment.ProtocolNumber, person?.FullName, deadlineDays);
                events.Add(new AlertEvent(acceptance.Id, deadlineDays, DateOnly.FromDateTime(acceptance.SentAt.UtcDateTime), subject, html, PersonEmail(person)));
            }
        }

        return await EmitAsync(organization, rule, AlertType.AssignmentNotConfirmed, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckOffboardingReturnDueAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.OffboardingReturnDue);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var cases = await _offboarding.ListOpenAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in NormalizeThresholds(rule))
        {
            var targetDate = today.AddDays(thresholdDays);
            foreach (var offboardingCase in cases.Where(c => DateOnly.FromDateTime(c.ReturnDueDate.UtcDateTime) <= targetDate))
            {
                var person = await _people.GetAsync(organization.Id, offboardingCase.PersonId, cancellationToken);
                var subject = $"Termin zwrotu w offboardingu — {person?.FullName ?? "—"}";
                var html = $"<p>Termin zwrotu sprzętu w sprawie offboardingowej osoby <strong>{Encode(person?.FullName)}</strong> to <strong>{offboardingCase.ReturnDueDate:yyyy-MM-dd}</strong>.</p>";
                events.Add(new AlertEvent(offboardingCase.Id, thresholdDays, DateOnly.FromDateTime(offboardingCase.ReturnDueDate.UtcDateTime), subject, html, PersonEmail(person)));
            }
        }

        return await EmitAsync(organization, rule, AlertType.OffboardingReturnDue, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckAssetAuditNoResponseAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.AssetAuditNoResponse);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var campaigns = await _auditCampaigns.ListActiveAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var campaign in campaigns)
        {
            var participants = await _auditParticipants.ListByCampaignAsync(organization.Id, campaign.Id, cancellationToken);
            var noResponse = participants.Where(p => p.Status is AssetAuditParticipantStatus.Pending or AssetAuditParticipantStatus.InProgress).ToList();
            if (noResponse.Count == 0) continue;

            var campaignDue = DateOnly.FromDateTime(campaign.DueDate.UtcDateTime);
            foreach (var thresholdDays in NormalizeThresholds(rule))
            {
                if (campaignDue > today.AddDays(thresholdDays)) continue;

                foreach (var participant in noResponse)
                {
                    var subject = $"Brak odpowiedzi w kampanii — {campaign.Name}";
                    var html = $"<p>Uczestnik <strong>{Encode(participant.Email)}</strong> nie odpowiedział w kampanii <strong>{Encode(campaign.Name)}</strong> (termin {campaign.DueDate:yyyy-MM-dd}).</p>";
                    events.Add(new AlertEvent(participant.Id, thresholdDays, campaignDue, subject, html, [participant.Email]));
                }
            }
        }

        return await EmitAsync(organization, rule, AlertType.AssetAuditNoResponse, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckReservationAwaitingApprovalAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.ReservationAwaitingApproval);
        if (rule is null) return false;

        var now = _clock.UtcNow;
        var reservations = await _reservations.ListOpenAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in NormalizeThresholds(rule))
        {
            foreach (var reservation in reservations.Where(r => r.Status == EquipmentReservationStatus.PendingApproval && r.RequestedAt.HasValue && r.RequestedAt.Value <= now.AddDays(-thresholdDays)))
            {
                var person = await _people.GetAsync(organization.Id, reservation.RequesterPersonId, cancellationToken);
                var subject = $"Rezerwacja oczekuje na akceptację — {reservation.Purpose}";
                var html = $"<p>Rezerwacja <strong>{Encode(reservation.Purpose)}</strong> osoby <strong>{Encode(person?.FullName)}</strong> czeka na akceptację od {reservation.RequestedAt:yyyy-MM-dd}.</p>";
                events.Add(new AlertEvent(reservation.Id, thresholdDays, DateOnly.FromDateTime(reservation.RequestedAt!.Value.UtcDateTime), subject, html, PersonEmail(person)));
            }
        }

        return await EmitAsync(organization, rule, AlertType.ReservationAwaitingApproval, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckReservationPickupUpcomingAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.ReservationPickupUpcoming);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var reservations = await _reservations.ListOpenAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in NormalizeThresholds(rule))
        {
            var targetDate = today.AddDays(thresholdDays);
            foreach (var reservation in reservations.Where(r => r.Status is EquipmentReservationStatus.Approved or EquipmentReservationStatus.ReadyForPickup))
            {
                var startDate = DateOnly.FromDateTime(reservation.StartAt.UtcDateTime);
                if (startDate < today || startDate > targetDate) continue;

                var person = await _people.GetAsync(organization.Id, reservation.RequesterPersonId, cancellationToken);
                var subject = $"Odbiór rezerwacji — {reservation.Purpose}";
                var html = $"<p>Odbiór sprzętu z rezerwacji <strong>{Encode(reservation.Purpose)}</strong> zaplanowano na <strong>{reservation.StartAt:yyyy-MM-dd}</strong>.</p>";
                events.Add(new AlertEvent(reservation.Id, thresholdDays, startDate, subject, html, PersonEmail(person)));
            }
        }

        return await EmitAsync(organization, rule, AlertType.ReservationPickupUpcoming, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckReservationOverdueAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.ReservationOverdue);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var reservations = await _reservations.ListOpenAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var reservation in reservations.Where(r => r.Status == EquipmentReservationStatus.CheckedOut))
        {
            var endDate = DateOnly.FromDateTime(reservation.EndAt.UtcDateTime);
            if (endDate >= today) continue;

            var person = await _people.GetAsync(organization.Id, reservation.RequesterPersonId, cancellationToken);
            var subject = $"Zwrot rezerwacji po terminie — {reservation.Purpose}";
            var html = $"<p>Termin zwrotu sprzętu z rezerwacji <strong>{Encode(reservation.Purpose)}</strong> minął ({reservation.EndAt:yyyy-MM-dd}).</p>";
            events.Add(new AlertEvent(reservation.Id, 0, endDate, subject, html, PersonEmail(person)));
        }

        return await EmitAsync(organization, rule, AlertType.ReservationOverdue, events, isQuietHours, digestItems, cancellationToken);
    }

    // ---------- digest ----------

    private async Task<bool> TryGenerateDigestAsync(Organization organization, List<DigestItem> items, bool isQuietHours, CancellationToken cancellationToken)
    {
        if (isQuietHours) return false;

        var digest = await _digestSettings.GetAsync(organization.Id, cancellationToken);
        if (digest is null || digest.Frequency == AlertDigestFrequency.Off) return false;
        if (!IsDigestDue(digest, organization, _clock.UtcNow)) return false;

        // Oznaczamy digest jako wygenerowany w tej lokalnej "porze dnia" — dzięki temu kolejne cykle w tej
        // samej dobie/week nie wyślą go ponownie.
        digest.Update(digest.Frequency, digest.DayOfWeek, digest.LocalTime, digest.QuietHoursStart, digest.QuietHoursEnd,
            digest.BusinessDays, digest.HolidayCalendarCountryCode, digest.IncludeEmptyDigest, _clock.UtcNow);

        if (items.Count == 0 && !digest.IncludeEmptyDigest) return true;

        var recipients = await GetAdminEmailsAsync(organization.Id, cancellationToken);
        if (recipients.Count == 0) return true;

        var (subject, html) = BuildDigestEmail(items);
        foreach (var email in recipients)
        {
            await _emailSender.SendAsync(email, subject, html, cancellationToken);
        }

        return true;
    }

    private bool IsDigestDue(AlertDigestSettings digest, Organization organization, DateTimeOffset nowUtc)
    {
        if (digest.Frequency == AlertDigestFrequency.Off) return false;

        var timeZone = GetOrganizationTimeZone(organization);
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        if (localNow.TimeOfDay < digest.LocalTime.ToTimeSpan()) return false;

        var todayLocal = DateOnly.FromDateTime(localNow.DateTime);
        DateOnly? lastLocal = digest.LastGeneratedAt is null
            ? null
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(digest.LastGeneratedAt.Value, timeZone).DateTime);

        return digest.Frequency switch
        {
            AlertDigestFrequency.Daily => lastLocal is null || lastLocal.Value < todayLocal,
            AlertDigestFrequency.Weekly => digest.DayOfWeek.HasValue
                && todayLocal.DayOfWeek == digest.DayOfWeek.Value
                && (lastLocal is null || lastLocal.Value < todayLocal),
            _ => false
        };
    }

    private static (string Subject, string Html) BuildDigestEmail(IReadOnlyList<DigestItem> items)
    {
        var subject = items.Count == 1
            ? "Tenebit — 1 działanie wymaga uwagi"
            : $"Tenebit — {items.Count} działań wymaga uwagi";

        var sections = items
            .GroupBy(i => i.Type)
            .OrderBy(g => g.Key)
            .Select(group =>
            {
                var title = group.Key switch
                {
                    AlertType.AssetWarrantyExpiring => "Gwarancje",
                    AlertType.LicenseExpiring => "Licencje",
                    AlertType.ProcedureReviewDue => "Procedury do przeglądu",
                    AlertType.AssignmentReturnDue => "Zwroty wydań",
                    AlertType.AssignmentNotConfirmed => "Niepotwierdzone wydania",
                    AlertType.OffboardingReturnDue => "Offboarding",
                    AlertType.AssetAuditNoResponse => "Kampanie aktywów",
                    AlertType.ReservationAwaitingApproval => "Rezerwacje oczekujące na akceptację",
                    AlertType.ReservationPickupUpcoming => "Odbiory rezerwacji",
                    AlertType.ReservationOverdue => "Zwroty rezerwacji po terminie",
                    _ => group.Key.ToString()
                };
                var rows = string.Join("", group.Select(i => $"<li>{Encode(i.Text)}{(i.DueDate.HasValue ? $" — termin {i.DueDate:yyyy-MM-dd}" : "")}</li>"));
                return $"<h3>{Encode(title)}</h3><ul>{rows}</ul>";
            });

        var html = $"<div style=\"font-family: sans-serif; max-width: 600px; margin: 0 auto;\"><h2>Podsumowanie alertów Tenebit</h2>{string.Join("", sections)}</div>";
        return (subject, html);
    }

    // ---------- shared helpers ----------

    private async Task<bool> EmitAsync(Organization organization, AlertRule rule, AlertType type, IReadOnlyList<AlertEvent> events, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var hasChanges = false;
        var includeInDigest = rule.DeliveryMode is AlertDeliveryMode.Digest or AlertDeliveryMode.Both;
        var sendImmediate = rule.DeliveryMode is AlertDeliveryMode.Immediate or AlertDeliveryMode.Both;

        foreach (var alertEvent in events)
        {
            if (includeInDigest)
            {
                digestItems.Add(new DigestItem(type, alertEvent.Subject, alertEvent.DueDate));
            }

            if (sendImmediate)
            {
                var recipients = await ResolveRecipientsAsync(organization, rule, alertEvent.ResponsibleEmails, cancellationToken);
                var alertKey = BuildAlertKey(type, alertEvent.ThresholdDays, alertEvent.DueDate);
                if (await DeliverAsync(organization.Id, alertKey, alertEvent.EntityId, recipients, alertEvent.Subject, alertEvent.Html, isQuietHours, cancellationToken))
                {
                    hasChanges = true;
                }
            }
        }

        return hasChanges;
    }

    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync(Organization organization, AlertRule rule, IReadOnlyList<string> responsibleEmails, CancellationToken cancellationToken)
    {
        return rule.RecipientMode switch
        {
            AlertRecipientMode.Custom => ParseCustomEmails(rule.CustomEmails),
            AlertRecipientMode.ResponsiblePerson => responsibleEmails.Count > 0 ? responsibleEmails : await GetAdminEmailsAsync(organization.Id, cancellationToken),
            AlertRecipientMode.ResponsibleRoles => await GetResponsibleRoleEmailsAsync(organization.Id, cancellationToken),
            _ => await GetAdminEmailsAsync(organization.Id, cancellationToken)
        };
    }

    private async Task<IReadOnlyList<string>> GetAdminEmailsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var users = await _users.ListAsync(organizationId, cancellationToken);
        return users
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role is TenebitRoles.Owner or TenebitRoles.Admin))
            .Select(u => u.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> GetResponsibleRoleEmailsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var users = await _users.ListAsync(organizationId, cancellationToken);
        return users
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role is TenebitRoles.Owner or TenebitRoles.Admin or TenebitRoles.AssetOperator or TenebitRoles.Technician))
            .Select(u => u.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<bool> DeliverAsync(Guid organizationId, string alertKey, Guid entityId, IEnumerable<string> recipients, string subject, string html, bool isQuietHours, CancellationToken cancellationToken)
    {
        var hasChanges = false;
        var now = _clock.UtcNow;

        foreach (var email in recipients.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var record = await _sentAlerts.GetAsync(organizationId, alertKey, entityId, email, cancellationToken);
            if (record is null)
            {
                record = new SentAlert(organizationId, alertKey, entityId, email, now);
                _sentAlerts.Add(record);
                hasChanges = true;
            }

            if (record.Status == SentAlertStatus.Sent) continue;
            if (record.Status == SentAlertStatus.Failed && !record.CanRetry(MaxSendAttempts, now)) continue;
            if (isQuietHours) continue;

            try
            {
                await _emailSender.SendAsync(email, subject, html, cancellationToken);
                record.MarkSent(now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                record.MarkFailed(now, ex.Message, RetryDelay);
            }

            hasChanges = true;
        }

        return hasChanges;
    }

    private static AlertRule? FindEnabledRule(IReadOnlyList<AlertRule> rules, AlertType type) =>
        rules.FirstOrDefault(r => r.Type == type && r.IsEnabled);

    private static IEnumerable<int> NormalizeThresholds(AlertRule rule) =>
        rule.ThresholdDays.Distinct().Where(d => d >= 0 && d <= AlertRule.MaxThresholdDays);

    // Format klucza dedup: `typ:próg:termin(yyyy-MM-dd)`. OrganizationId/EntityId/RecipientEmail są już
    // osobnymi kolumnami SentAlert, więc AlertKey koduje tylko to, co się zmienia przy zmianie progu/terminu.
    private static string BuildAlertKey(AlertType type, int thresholdDays, DateOnly? dueDate) =>
        $"{type}:{thresholdDays}:{(dueDate.HasValue ? dueDate.Value.ToString("yyyy-MM-dd") : "none")}";

    private static TimeZoneInfo GetOrganizationTimeZone(Organization organization)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(organization.TimeZone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static IReadOnlyList<string> ParseCustomEmails(string? customEmails) =>
        (customEmails ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> PersonEmail(Person? person) =>
        person is not null && !string.IsNullOrWhiteSpace(person.Email) ? [person.Email] : [];

    private static string Encode(string? value) => value is null ? string.Empty : WebUtility.HtmlEncode(value);

    private sealed record AlertEvent(Guid EntityId, int ThresholdDays, DateOnly? DueDate, string Subject, string Html, IReadOnlyList<string> ResponsibleEmails);

    private sealed record DigestItem(AlertType Type, string Text, DateOnly? DueDate);
}

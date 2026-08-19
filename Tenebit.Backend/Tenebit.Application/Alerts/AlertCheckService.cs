using System.Net;
using System.Security.Cryptography;
using System.Text;
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
    private readonly ILicenseRepository _licenses;
    private readonly IPersonRepository _people;
    private readonly ISentAlertRepository _sentAlerts;
    private readonly IAlertRuleRepository _rules;
    private readonly IAlertDigestSettingsRepository _digestSettings;
    private readonly IOffboardingCaseRepository _offboarding;
    private readonly IAssetAuditCampaignRepository _auditCampaigns;
    private readonly IAssetAuditParticipantRepository _auditParticipants;
    private readonly IEquipmentReservationRepository _reservations;
    private readonly IEmailSender _emailSender;
    private readonly IEmailOutboxWriter? _emailOutbox;
    private readonly Dictionary<Guid, IReadOnlyList<Tenebit.Domain.Identity.OrganizationUser>> _usersByOrganization = [];
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AlertCheckService(
        IOrganizationRepository organizations,
        IOrganizationUserRepository users,
        IAssetRepository assets,
        IAssignmentRepository assignments,
        IProcedureRepository procedures,
        ILicenseRepository licenses,
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
        IUnitOfWork unitOfWork,
        IEmailOutboxWriter? emailOutbox = null)
    {
        _organizations = organizations;
        _users = users;
        _assets = assets;
        _assignments = assignments;
        _procedures = procedures;
        _licenses = licenses;
        _people = people;
        _sentAlerts = sentAlerts;
        _rules = rules;
        _digestSettings = digestSettings;
        _offboarding = offboarding;
        _auditCampaigns = auditCampaigns;
        _auditParticipants = auditParticipants;
        _reservations = reservations;
        _emailSender = emailSender;
        _emailOutbox = emailOutbox;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    // onboardingDeadlineDays is read from configuration by the caller (AlertBackgroundService).
    public async Task RunAsync(int onboardingDeadlineDays, CancellationToken cancellationToken)
    {
        var organizations = await _organizations.ListAllAsync(cancellationToken);
        foreach (var organization in organizations)
        {
            await ProcessOrganizationAsync(organization, onboardingDeadlineDays, cancellationToken);
        }
    }

    public async Task RunOrganizationAsync(Guid organizationId, int onboardingDeadlineDays, CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetAsync(organizationId, cancellationToken);
        if (organization is null) return;
        await ProcessOrganizationAsync(organization, onboardingDeadlineDays, cancellationToken);
    }

    private async Task ProcessOrganizationAsync(Organization organization, int onboardingDeadlineDays, CancellationToken cancellationToken)
    {
        _usersByOrganization.Remove(organization.Id);
        var rules = await _rules.ListByOrganizationAsync(organization.Id, cancellationToken);
        if (rules.Count == 0) return;

        var isQuietHours = organization.IsWithinQuietHours(_clock.UtcNow);
        var digestItems = new List<DigestItem>();
        var hasChanges = false;

        hasChanges |= await CheckWarrantyAlertsAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckLicenseExpiringAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckProcedureReviewDueAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckAssignmentReturnDueAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckAssignmentNotConfirmedAsync(organization, onboardingDeadlineDays, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckOffboardingReturnDueAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckAssetAuditNoResponseAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckReservationAwaitingApprovalAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckReservationPickupUpcomingAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await CheckReservationOverdueAsync(organization, rules, isQuietHours, digestItems, cancellationToken);
        hasChanges |= await TryGenerateDigestAsync(organization, digestItems, isQuietHours, cancellationToken);

        if (hasChanges) await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------- detection methods ----------

    private async Task<bool> CheckWarrantyAlertsAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.AssetWarrantyExpiring);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var thresholds = NormalizeThresholds(rule).ToArray();
        if (thresholds.Length == 0) return false;
        var assets = await _assets.ListWarrantyExpiringAsync(organization.Id, today, today.AddDays(thresholds.Max()), cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in thresholds)
        {
            var targetDate = today.AddDays(thresholdDays);
            foreach (var asset in assets.Where(a => a.WarrantyUntil.HasValue && a.WarrantyUntil.Value >= today && a.WarrantyUntil.Value <= targetDate))
            {
                var subject = $"Gwarancja wygasa za {thresholdDays} dni - {asset.Name} ({asset.AssetTag})";
                var html = $"<p>Gwarancja na aktywo <strong>{Encode(asset.Name)}</strong> (tag: {Encode(asset.AssetTag)}) wygasa <strong>{asset.WarrantyUntil:yyyy-MM-dd}</strong> ({thresholdDays} dni).</p>";
                events.Add(new AlertEvent(asset.Id, thresholdDays, asset.WarrantyUntil, subject, html, []));
            }
        }

        return await EmitAsync(organization, rule, AlertType.AssetWarrantyExpiring, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckLicenseExpiringAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.LicenseExpiring);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var licenses = await _licenses.ListAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in NormalizeThresholds(rule))
        {
            var targetDate = today.AddDays(thresholdDays);
            foreach (var license in licenses.Where(l => l.ExpiresAt.HasValue && l.ExpiresAt.Value >= today && l.ExpiresAt.Value <= targetDate))
            {
                // Spec 7.9: alert o licencji NIE ujawnia klucza licencyjnego - tylko nazwa, dostawca i data.
                var vendorPart = license.Vendor is null ? string.Empty : $" (dostawca: {Encode(license.Vendor)})";
                var subject = $"Licencja wygasa za {thresholdDays} dni - {license.Name}";
                var html = $"<p>Licencja <strong>{Encode(license.Name)}</strong>{vendorPart} wygasa <strong>{license.ExpiresAt:yyyy-MM-dd}</strong> ({thresholdDays} dni).</p>";
                events.Add(new AlertEvent(license.Id, thresholdDays, license.ExpiresAt, subject, html, []));
            }
        }

        return await EmitAsync(organization, rule, AlertType.LicenseExpiring, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckProcedureReviewDueAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.ProcedureReviewDue);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var procedures = await _procedures.ListAsync(organization.Id, null, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var thresholdDays in NormalizeThresholds(rule))
        {
            var targetDate = today.AddDays(thresholdDays);
            foreach (var procedure in procedures.Where(p => p.ReviewDate.HasValue && p.ReviewDate.Value >= today && p.ReviewDate.Value <= targetDate))
            {
                var subject = $"Termin przeglądu procedury - {procedure.Title}";
                var html = $"<p>Procedura <strong>{Encode(procedure.Title)}</strong> (wersja {Encode(procedure.Version)}) wymaga przeglądu do <strong>{procedure.ReviewDate:yyyy-MM-dd}</strong> ({thresholdDays} dni).</p>";
                events.Add(new AlertEvent(procedure.Id, thresholdDays, procedure.ReviewDate, subject, html, []));
            }
        }

        return await EmitAsync(organization, rule, AlertType.ProcedureReviewDue, events, isQuietHours, digestItems, cancellationToken);
    }

    private async Task<bool> CheckAssignmentReturnDueAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.AssignmentReturnDue);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var assignments = await _assignments.ListAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var assignment in assignments.Where(a => a.DueDate.HasValue && a.Status is AssignmentStatus.AwaitingAcceptance or AssignmentStatus.Accepted))
        {
            var due = assignment.DueDate!.Value;
            var person = await _people.GetAsync(organization.Id, assignment.PersonId, cancellationToken);
            foreach (var (thresholdDays, _) in DueDateThresholds(rule, today, due))
            {
                var (subject, html) = thresholdDays == 0
                    ? ($"Zwrot sprzętu po terminie - wydanie {assignment.ProtocolNumber}",
                       $"<p>Termin zwrotu sprzętu z wydania <strong>{Encode(assignment.ProtocolNumber)}</strong> dla osoby <strong>{Encode(person?.FullName)}</strong> minął ({due:yyyy-MM-dd}).</p>")
                    : ($"Termin zwrotu wydania za {thresholdDays} dni - wydanie {assignment.ProtocolNumber}",
                       $"<p>Termin zwrotu sprzętu z wydania <strong>{Encode(assignment.ProtocolNumber)}</strong> dla osoby <strong>{Encode(person?.FullName)}</strong> to <strong>{due:yyyy-MM-dd}</strong>.</p>");
                events.Add(new AlertEvent(assignment.Id, thresholdDays, due, subject, html, PersonEmail(person)));
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
        var hasChanges = false;

        foreach (var assignment in assignments.Where(a => a.Status == AssignmentStatus.AwaitingAcceptance && a.IssuedAt <= deadline))
        {
            // ListAsync zwraca encje AsNoTracking - MarkOverdue na nich nie zapisałby statusu. Pobieramy
            // śledzoną kopię i dopiero na niej oznaczamy Overdue (Poprawka 4), żeby dashboard to widział.
            var tracked = await _assignments.GetAsync(organization.Id, assignment.Id, cancellationToken);
            if (tracked is not null && tracked.Status == AssignmentStatus.AwaitingAcceptance)
            {
                tracked.MarkOverdue();
                hasChanges = true;
            }

            var person = await _people.GetAsync(organization.Id, assignment.PersonId, cancellationToken);
            var subject = $"Sprzęt nieodebrany - wydanie {assignment.ProtocolNumber}";
            var html = $"<p>Sprzęt z wydania <strong>{Encode(assignment.ProtocolNumber)}</strong> dla osoby <strong>{Encode(person?.FullName)}</strong> nie został odebrany w ciągu {deadlineDays} dni od wysłania.</p>";
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
                var trackedAssignment = await _assignments.GetAsync(organization.Id, assignment.Id, cancellationToken);
                var trackedAcceptance = trackedAssignment?.ProcedureAcceptances.FirstOrDefault(p => p.Id == acceptance.Id);
                if (trackedAcceptance is not null && trackedAcceptance.Status == AcceptanceStatus.Pending)
                {
                    trackedAcceptance.MarkOverdue();
                    hasChanges = true;
                }

                var person = await _people.GetAsync(organization.Id, acceptance.PersonId, cancellationToken);
                var procedure = procedures.FirstOrDefault(x => x.Id == acceptance.ProcedureId);
                var (subject, html) = EmailTemplates.ProcedureUnsignedAlert(organization.Language, procedure?.Title, assignment.ProtocolNumber, person?.FullName, deadlineDays);
                events.Add(new AlertEvent(acceptance.Id, deadlineDays, DateOnly.FromDateTime(acceptance.SentAt.UtcDateTime), subject, html, PersonEmail(person)));
            }
        }

        hasChanges |= await EmitAsync(organization, rule, AlertType.AssignmentNotConfirmed, events, isQuietHours, digestItems, cancellationToken);
        return hasChanges;
    }

    private async Task<bool> CheckOffboardingReturnDueAsync(Organization organization, IReadOnlyList<AlertRule> rules, bool isQuietHours, List<DigestItem> digestItems, CancellationToken cancellationToken)
    {
        var rule = FindEnabledRule(rules, AlertType.OffboardingReturnDue);
        if (rule is null) return false;

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var cases = await _offboarding.ListOpenAsync(organization.Id, cancellationToken);
        var events = new List<AlertEvent>();

        foreach (var offboardingCase in cases)
        {
            var due = DateOnly.FromDateTime(offboardingCase.ReturnDueDate.UtcDateTime);
            var person = await _people.GetAsync(organization.Id, offboardingCase.PersonId, cancellationToken);
            foreach (var (thresholdDays, _) in DueDateThresholds(rule, today, due))
            {
                var (subject, html) = thresholdDays == 0
                    ? ($"Termin zwrotu w offboardingu minął - {person?.FullName ?? "-"}",
                       $"<p>Termin zwrotu sprzętu w sprawie offboardingowej osoby <strong>{Encode(person?.FullName)}</strong> minął ({offboardingCase.ReturnDueDate:yyyy-MM-dd}).</p>")
                    : ($"Termin zwrotu w offboardingu za {thresholdDays} dni - {person?.FullName ?? "-"}",
                       $"<p>Termin zwrotu sprzętu w sprawie offboardingowej osoby <strong>{Encode(person?.FullName)}</strong> to <strong>{offboardingCase.ReturnDueDate:yyyy-MM-dd}</strong>.</p>");
                events.Add(new AlertEvent(offboardingCase.Id, thresholdDays, due, subject, html, PersonEmail(person)));
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
                    var subject = $"Brak odpowiedzi w kampanii - {campaign.Name}";
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
                var subject = $"Rezerwacja oczekuje na akceptację - {reservation.Purpose}";
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
                var subject = $"Odbiór rezerwacji - {reservation.Purpose}";
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
            var subject = $"Zwrot rezerwacji po terminie - {reservation.Purpose}";
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

        // Oznaczamy digest jako wygenerowany w tej lokalnej "porze dnia" - dzięki temu kolejne cykle w tej
        // samej dobie/week nie wyślą go ponownie.
        digest.Update(digest.Frequency, digest.DayOfWeek, digest.LocalTime, digest.QuietHoursStart, digest.QuietHoursEnd,
            digest.BusinessDays, digest.HolidayCalendarCountryCode, digest.IncludeEmptyDigest, _clock.UtcNow);

        if (items.Count == 0 && !digest.IncludeEmptyDigest) return true;

        var recipients = await GetAdminEmailsAsync(organization.Id, cancellationToken);
        if (recipients.Count == 0) return true;

        var (subject, html) = BuildDigestEmail(items);
        foreach (var email in recipients)
        {
            if (_emailOutbox is not null)
            {
                var localDate = TimeZoneInfo.ConvertTime(_clock.UtcNow, GetOrganizationTimeZone(organization)).Date;
                var digestKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email.ToLowerInvariant()}|{localDate:yyyy-MM-dd}|{digest.Frequency}")));
                await _emailOutbox.EnqueueAsync(organization.Id, email, subject, html, "alert-digest", $"alert-digest:{organization.Id:N}:{digestKey}", cancellationToken);
            }
            else
            {
                await _emailSender.SendAsync(email, subject, html, cancellationToken);
            }
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

        // BusinessDays (spec 7.3 flags) - digest wychodzi tylko w skonfigurowane dni. HolidayCalendarCountryCode
        // jest celowo jeszcze nieużywane: honorowanie świąt wymagałoby zewnętrznego źródła kalendarza (osobne zadanie).
        if (!digest.BusinessDays.HasFlag(ToBusinessDayFlag(todayLocal.DayOfWeek))) return false;

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
            ? "Tenebit - 1 działanie wymaga uwagi"
            : $"Tenebit - {items.Count} działań wymaga uwagi";

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
                var rows = string.Join("", group.Select(i => $"<li>{Encode(i.Text)}{(i.DueDate.HasValue ? $" - termin {i.DueDate:yyyy-MM-dd}" : "")}</li>"));
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
                var alertKey = BuildAlertKey(type, alertEvent.ThresholdDays, alertEvent.DueDate);

                // CooldownDays (spec 7.4): ten sam byt nie dostaje kolejnego alertu częściej niż raz na
                // CooldownDays - nawet gdy trafi go inny próg/termin (inny klucz dedup). Retry/dedup tego
                // samego klucza obsługuje DeliverAsync, więc cooldown go nie tłumi.
                if (await IsWithinCooldownAsync(organization.Id, rule, type, alertEvent.EntityId, alertKey, cancellationToken))
                {
                    continue;
                }

                var recipients = await ResolveRecipientsAsync(organization, rule, alertEvent.ResponsibleEmails, cancellationToken);
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
            // Domyślny tryb „właściciele+adminowie" musi obejmować też odpowiedzialną osobę, jeśli istnieje -
            // przed #23 alerty zwrotu/niepotwierdzenia zawsze dorzucały email osoby powiązanej z wydaniem.
            _ => (await GetAdminEmailsAsync(organization.Id, cancellationToken))
                .Concat(responsibleEmails)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private async Task<IReadOnlyList<string>> GetAdminEmailsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var users = await GetOrganizationUsersAsync(organizationId, cancellationToken);
        return users
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role is TenebitRoles.Owner or TenebitRoles.Admin))
            .Select(u => u.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> GetResponsibleRoleEmailsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var users = await GetOrganizationUsersAsync(organizationId, cancellationToken);
        return users
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role is TenebitRoles.Owner or TenebitRoles.Admin or TenebitRoles.AssetOperator or TenebitRoles.Technician))
            .Select(u => u.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<Tenebit.Domain.Identity.OrganizationUser>> GetOrganizationUsersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (_usersByOrganization.TryGetValue(organizationId, out var cached)) return cached;
        var users = await _users.ListAsync(organizationId, cancellationToken);
        _usersByOrganization[organizationId] = users;
        return users;
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
                if (_emailOutbox is not null)
                {
                    var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{alertKey}|{email.ToLowerInvariant()}")));
                    await _emailOutbox.EnqueueAsync(organizationId, email, subject, html, "alert", $"alert:{organizationId:N}:{entityId:N}:{digest}", cancellationToken);
                }
                else
                {
                    await _emailSender.SendAsync(email, subject, html, cancellationToken);
                }
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

    // Terminowe typy (zwrot wydania/offboardingu): „przypomnienie N dni przed terminem" dla każdego progu
    // N > 0 (okno jak gwarancja) OSOBNO od stanu „już po terminie" (jeden alert, próg 0, bez powtórek per próg).
    private static IEnumerable<(int Threshold, DateOnly DueDate)> DueDateThresholds(AlertRule rule, DateOnly today, DateOnly dueDate)
    {
        if (dueDate <= today)
        {
            yield return (0, dueDate);
            yield break;
        }

        foreach (var threshold in NormalizeThresholds(rule).Where(t => t > 0))
        {
            if (dueDate <= today.AddDays(threshold))
            {
                yield return (threshold, dueDate);
            }
        }
    }

    private static AlertDigestBusinessDays ToBusinessDayFlag(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => AlertDigestBusinessDays.Monday,
        DayOfWeek.Tuesday => AlertDigestBusinessDays.Tuesday,
        DayOfWeek.Wednesday => AlertDigestBusinessDays.Wednesday,
        DayOfWeek.Thursday => AlertDigestBusinessDays.Thursday,
        DayOfWeek.Friday => AlertDigestBusinessDays.Friday,
        DayOfWeek.Saturday => AlertDigestBusinessDays.Saturday,
        DayOfWeek.Sunday => AlertDigestBusinessDays.Sunday,
        _ => AlertDigestBusinessDays.None
    };

    private async Task<bool> IsWithinCooldownAsync(Guid organizationId, AlertRule rule, AlertType type, Guid entityId, string currentKey, CancellationToken cancellationToken)
    {
        if (rule.CooldownDays <= 0) return false;
        var latest = await _sentAlerts.GetLatestAsync(organizationId, entityId, $"{type}:", cancellationToken);
        if (latest is null) return false;
        // Ten sam klucz = retry/dedup tego samego alertu - tym zajmuje się DeliverAsync, nie cooldown.
        if (latest.AlertKey == currentKey) return false;
        return latest.CreatedAt >= _clock.UtcNow.AddDays(-rule.CooldownDays);
    }

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

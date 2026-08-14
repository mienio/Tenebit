using Tenebit.Application.Abstractions;
using Tenebit.Application.Common;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Organizations;

namespace Tenebit.Application.Alerts;

public sealed class AlertCheckService
{
    private static readonly int[] WarrantyThresholdDays = [30, 7];
    private const int MaxSendAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationUserRepository _users;
    private readonly IAssetRepository _assets;
    private readonly IAssignmentRepository _assignments;
    private readonly IProcedureRepository _procedures;
    private readonly IPersonRepository _people;
    private readonly ISentAlertRepository _sentAlerts;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AlertCheckService(IOrganizationRepository organizations, IOrganizationUserRepository users, IAssetRepository assets, IAssignmentRepository assignments, IProcedureRepository procedures, IPersonRepository people, ISentAlertRepository sentAlerts, IEmailSender emailSender, IClock clock, IUnitOfWork unitOfWork)
    {
        _organizations = organizations;
        _users = users;
        _assets = assets;
        _assignments = assignments;
        _procedures = procedures;
        _people = people;
        _sentAlerts = sentAlerts;
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
            var isQuietHours = organization.IsWithinQuietHours(_clock.UtcNow);
            await CheckWarrantyAlertsAsync(organization, isQuietHours, cancellationToken);
            await CheckOverdueAssignmentsAsync(organization, isQuietHours, cancellationToken);
            await CheckOnboardingDeadlinesAsync(organization, onboardingDeadlineDays, isQuietHours, cancellationToken);
        }
    }

    private async Task CheckWarrantyAlertsAsync(Organization organization, bool isQuietHours, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var assets = await _assets.ListAsync(organization.Id, null, null, null, cancellationToken);
        var adminEmails = await GetAdminEmailsAsync(organization.Id, cancellationToken);
        var hasChanges = false;

        foreach (var thresholdDays in WarrantyThresholdDays)
        {
            var targetDate = today.AddDays(thresholdDays);
            // "<=" (not "==") so a missed run (restart/deploy/downtime) still catches the alert on the
            // next run instead of permanently skipping it — dedup against sent_alerts keeps it a one-time send.
            foreach (var asset in assets.Where(a => a.WarrantyUntil.HasValue && a.WarrantyUntil.Value <= targetDate && a.WarrantyUntil.Value >= today))
            {
                var alertKey = $"warranty_{thresholdDays}d";
                var subject = $"Gwarancja wygasa za {thresholdDays} dni — {asset.Name} ({asset.AssetTag})";
                var html = $"""
                    <p>Gwarancja na aktywo <strong>{System.Net.WebUtility.HtmlEncode(asset.Name)}</strong> (tag: {System.Net.WebUtility.HtmlEncode(asset.AssetTag)}) wygasa <strong>{asset.WarrantyUntil:yyyy-MM-dd}</strong> ({thresholdDays} dni).</p>
                    """;

                if (await DeliverAsync(organization.Id, alertKey, asset.Id, adminEmails, subject, html, isQuietHours, cancellationToken))
                {
                    hasChanges = true;
                }
            }
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CheckOverdueAssignmentsAsync(Organization organization, bool isQuietHours, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var assignments = await _assignments.ListAsync(organization.Id, cancellationToken);
        var overdue = assignments.Where(a => a.DueDate.HasValue && a.DueDate.Value < today && a.Status is AssignmentStatus.AwaitingAcceptance or AssignmentStatus.Accepted).ToList();
        if (overdue.Count == 0) return;

        var adminEmails = await GetAdminEmailsAsync(organization.Id, cancellationToken);
        var hasChanges = false;

        foreach (var assignment in overdue)
        {
            var alertKey = "assignment_overdue";
            var person = await _people.GetAsync(organization.Id, assignment.PersonId, cancellationToken);
            var subject = $"Zwrot sprzętu po terminie — protokół {assignment.ProtocolNumber}";
            var html = $"""
                <p>Termin zwrotu sprzętu z protokołu <strong>{System.Net.WebUtility.HtmlEncode(assignment.ProtocolNumber)}</strong> dla osoby <strong>{System.Net.WebUtility.HtmlEncode(person?.FullName ?? "—")}</strong> minął ({assignment.DueDate:yyyy-MM-dd}).</p>
                """;

            var recipients = adminEmails.ToList();
            if (person is not null && !string.IsNullOrWhiteSpace(person.Email)) recipients.Add(person.Email);

            if (await DeliverAsync(organization.Id, alertKey, assignment.Id, recipients, subject, html, isQuietHours, cancellationToken))
            {
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // Onboarding deadlines: an asset not collected (assignment still AwaitingAcceptance) or a procedure not
    // signed (acceptance still Pending) within a configurable number of days is flagged Overdue and triggers
    // one reminder email each (deduped via sent_alerts, same pattern as the other alert checks above).
    private async Task CheckOnboardingDeadlinesAsync(Organization organization, int deadlineDays, bool isQuietHours, CancellationToken cancellationToken)
    {
        deadlineDays = Math.Max(1, deadlineDays);
        var deadline = _clock.UtcNow.AddDays(-deadlineDays);
        var assignments = await _assignments.ListAsync(organization.Id, cancellationToken);
        var adminEmails = await GetAdminEmailsAsync(organization.Id, cancellationToken);
        var hasChanges = false;

        foreach (var assignment in assignments.Where(a => a.Status == AssignmentStatus.AwaitingAcceptance && a.IssuedAt <= deadline))
        {
            assignment.MarkOverdue();
            hasChanges = true;

            var alertKey = "assignment_pickup_overdue";
            var person = await _people.GetAsync(organization.Id, assignment.PersonId, cancellationToken);
            var subject = $"Sprzęt nieodebrany — protokół {assignment.ProtocolNumber}";
            var html = $"""
                <p>Sprzęt z protokołu <strong>{System.Net.WebUtility.HtmlEncode(assignment.ProtocolNumber)}</strong> dla osoby <strong>{System.Net.WebUtility.HtmlEncode(person?.FullName ?? "—")}</strong> nie został odebrany w ciągu {deadlineDays} dni od wysłania.</p>
                """;

            var recipients = adminEmails.ToList();
            if (person is not null && !string.IsNullOrWhiteSpace(person.Email)) recipients.Add(person.Email);
            if (await DeliverAsync(organization.Id, alertKey, assignment.Id, recipients, subject, html, isQuietHours, cancellationToken))
            {
                hasChanges = true;
            }
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
                acceptance.MarkOverdue();
                hasChanges = true;

                var alertKey = "procedure_signature_overdue";
                var person = await _people.GetAsync(organization.Id, acceptance.PersonId, cancellationToken);
                var procedure = procedures.FirstOrDefault(x => x.Id == acceptance.ProcedureId);
                var (subject, html) = EmailTemplates.ProcedureUnsignedAlert(organization.Language, procedure?.Title, assignment.ProtocolNumber, person?.FullName, deadlineDays);

                var recipients = adminEmails.ToList();
                if (person is not null && !string.IsNullOrWhiteSpace(person.Email)) recipients.Add(person.Email);
                if (await DeliverAsync(organization.Id, alertKey, acceptance.Id, recipients, subject, html, isQuietHours, cancellationToken))
                {
                    hasChanges = true;
                }
            }
        }

        if (hasChanges)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
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
}

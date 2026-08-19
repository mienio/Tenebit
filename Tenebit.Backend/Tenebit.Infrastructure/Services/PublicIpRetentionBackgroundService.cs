using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tenebit.Application.Common;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Organizations;
using Tenebit.Infrastructure.Data;

namespace Tenebit.Infrastructure.Services;

/// <summary>Enforces CapturePublicIp and PublicIpRetentionDays on both new and historical captured IP data.</summary>
public sealed class PublicIpRetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PublicIpRetentionBackgroundService> _logger;

    public PublicIpRetentionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PublicIpRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gate = scope.ServiceProvider.GetRequiredService<PostgresJobLock>();
                var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<Tenebit.Application.Abstractions.IClock>();
                await gate.TryRunAsync("public-ip-retention", Interval, ct => RunAsync(db, clock.UtcNow, ct), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Public IP retention cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task RunAsync(TenebitDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.ActivityLogs.Where(x => x.SourceIpExpiresAt != null && x.SourceIpExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SourceIp, (string?)null).SetProperty(x => x.SourceIpExpiresAt, (DateTimeOffset?)null), cancellationToken);

        var organizations = await db.Organizations.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var organization in organizations)
        {
            var assignments = await db.Assignments
                .Include(x => x.Assets)
                .Include(x => x.ProcedureAcceptances)
                .Where(x => x.OrganizationId == organization.Id && x.AcceptedAt != null && x.AcceptanceHash != null)
                .ToListAsync(cancellationToken);
            if (assignments.Count == 0) continue;

            var assignmentIds = assignments.Select(x => x.Id).ToArray();
            var evidence = await db.AssetEvidence.AsNoTracking()
                .Where(x => x.OrganizationId == organization.Id && x.AssignmentId != null && assignmentIds.Contains(x.AssignmentId.Value) && x.Phase == EvidencePhase.Issue)
                .Select(x => new { AssignmentId = x.AssignmentId!.Value, Entry = new AssetEvidenceIntegrityEntry(x.Id, x.Phase, x.Sha256) })
                .ToListAsync(cancellationToken);
            var byAssignment = evidence.GroupBy(x => x.AssignmentId).ToDictionary(g => g.Key, g => (IReadOnlyList<AssetEvidenceIntegrityEntry>)g.Select(x => x.Entry).ToList());

            foreach (var assignment in assignments)
            {
                var capturedAt = assignment.AcceptedAt!.Value;
                var capture = PublicIpPrivacyPolicy.Capture(organization, assignment.AcceptedIp, capturedAt);
                var desired = capture.ExpiresAt.HasValue && capture.ExpiresAt <= now ? null : capture.StoredIp;
                byAssignment.TryGetValue(assignment.Id, out var assignmentEvidence);
                if (!string.Equals(desired, assignment.AcceptedIp, StringComparison.Ordinal) || assignment.IntegrityVersion < 3)
                    assignment.ApplyAcceptedIpPrivacyWithEvidenceIntegrity(desired, assignmentEvidence);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

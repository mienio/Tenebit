using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Common;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Infrastructure.Data;

public sealed class TenebitDbContext : DbContext, IUnitOfWork
{
    private readonly IFieldEncryptor _fieldEncryptor;
    private readonly ITenantContext? _tenantContext;

    public TenebitDbContext(DbContextOptions<TenebitDbContext> options, IFieldEncryptor fieldEncryptor, ITenantContext? tenantContext = null) : base(options)
    {
        _fieldEncryptor = fieldEncryptor;
        _tenantContext = tenantContext;
    }

    private Guid CurrentTenantOrganizationId => _tenantContext?.OrganizationId ?? Guid.Empty;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Dane zostały zmodyfikowane równolegle - odśwież i spróbuj ponownie.");
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public async Task<T> ExecuteWithResourceLocksAsync<T>(Guid organizationId, string resourceType, IReadOnlyCollection<Guid> resourceIds, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        var orderedIds = resourceIds.Distinct().OrderBy(x => x).ToArray();
        if (orderedIds.Length == 0) return await ExecuteInTransactionAsync(action, cancellationToken);

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            foreach (var resourceId in orderedIds)
            {
                await AcquireAdvisoryLockAsync($"{organizationId:N}:{resourceType}:{resourceId:N}", cancellationToken);
            }

            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private Task<int> AcquireAdvisoryLockAsync(string key, CancellationToken cancellationToken) =>
        Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock((('x' || substr(md5({key}), 1, 16))::bit(64)::bigint))",
            cancellationToken);

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<AssetInspection> AssetInspections => Set<AssetInspection>();
    public DbSet<ServiceTicket> ServiceTickets => Set<ServiceTicket>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<PersonRelationType> PersonRelationTypes => Set<PersonRelationType>();
    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<ProcedureDocument> ProcedureDocuments => Set<ProcedureDocument>();
    public DbSet<JobProfile> JobProfiles => Set<JobProfile>();
    public DbSet<OrganizationUser> OrganizationUsers => Set<OrganizationUser>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OAuthTransaction> OAuthTransactions => Set<OAuthTransaction>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<DeviceTrustToken> DeviceTrustTokens => Set<DeviceTrustToken>();
    public DbSet<TwoFactorRecoveryCode> TwoFactorRecoveryCodes => Set<TwoFactorRecoveryCode>();
    public DbSet<AssetStatusSetting> AssetStatusSettings => Set<AssetStatusSetting>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<OrganizationSubscription> Subscriptions => Set<OrganizationSubscription>();
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents => Set<ProcessedStripeEvent>();
    public DbSet<SentAlert> SentAlerts => Set<SentAlert>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertDigestSettings> AlertDigestSettings => Set<AlertDigestSettings>();
    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();
    public DbSet<DashboardSnapshot> DashboardSnapshots => Set<DashboardSnapshot>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AssetEvidence> AssetEvidence => Set<AssetEvidence>();
    public DbSet<OffboardingCase> OffboardingCases => Set<OffboardingCase>();
    public DbSet<OffboardingItem> OffboardingItems => Set<OffboardingItem>();
    public DbSet<AssetAuditCampaign> AssetAuditCampaigns => Set<AssetAuditCampaign>();
    public DbSet<AssetAuditParticipant> AssetAuditParticipants => Set<AssetAuditParticipant>();
    public DbSet<AssetAuditItem> AssetAuditItems => Set<AssetAuditItem>();
    public DbSet<EquipmentReservation> EquipmentReservations => Set<EquipmentReservation>();
    public DbSet<EquipmentReservationItem> EquipmentReservationItems => Set<EquipmentReservationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenebit");
        ConfigureOrganizations(modelBuilder);
        ConfigureIdentity(modelBuilder);
        ConfigureAssets(modelBuilder);
        ConfigureLocations(modelBuilder);
        ConfigurePeople(modelBuilder);
        ConfigureProcedures(modelBuilder);
        ConfigureJobProfiles(modelBuilder);
        ConfigureSettings(modelBuilder);
        ConfigureAssignments(modelBuilder);
        ConfigureActivity(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigureAlerts(modelBuilder);
        ConfigureDashboards(modelBuilder);
        ConfigureLicenses(modelBuilder);
        ConfigureAssetEvidence(modelBuilder);
        ConfigureOffboarding(modelBuilder);
        ConfigureAudits(modelBuilder);
        ConfigureReservations(modelBuilder);
        ConfigureTenantQueryFilters(modelBuilder);
    }

    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        // Defense in depth: authenticated request queries are automatically tenant-scoped. Background/public
        // flows have no authenticated tenant and therefore use the explicit repository filters already present.
        var configureMethod = typeof(TenebitDbContext).GetMethod(nameof(ConfigureTenantQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.BaseType is not null) continue;
            var organizationId = entityType.FindProperty("OrganizationId");
            if (organizationId?.ClrType != typeof(Guid)) continue;

            configureMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureTenantQueryFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
            CurrentTenantOrganizationId == Guid.Empty ||
            EF.Property<Guid>(entity, "OrganizationId") == CurrentTenantOrganizationId);

    private static void ConfigureAudits(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetAuditCampaign>(entity =>
        {
            entity.ToTable("asset_audit_campaigns");
            entity.HasKey(x => x.Id);
            // AUD-003: alternate key (OrganizationId, Id) - pozwala dzieciom (participant/item) mieć composite FK,
            // więc baza odrzuci wiersz, który wskazuje kampanię innej organizacji niż deklarowana w OrganizationId dziecka.
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.CreatedBy).HasMaxLength(240).IsRequired();
            entity.Property(x => x.CompletedBy).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
        });

        modelBuilder.Entity<AssetAuditParticipant>(entity =>
        {
            entity.ToTable("asset_audit_participants");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.HasIndex(x => new { x.OrganizationId, x.CampaignId, x.PersonId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.PersonId }).HasDatabaseName("IX_tenant_audit_participants_person");
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.PersonId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);
            // Indexed lookup for the public audit link - without it ResolveByTokenAsync had to load every
            // participant with a live token and verify each one in turn (audyt AUD3-009: O(N) koszt
            // rosnący z liczbą uczestników, wykorzystywalny jako publiczny DoS).
            entity.HasIndex(x => x.TokenHash)
                .IsUnique()
                .HasFilter("\"TokenHash\" IS NOT NULL")
                .HasDatabaseName("IX_asset_audit_participants_TokenHash");
            entity.HasOne<AssetAuditCampaign>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.CampaignId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetAuditItem>(entity =>
        {
            entity.ToTable("asset_audit_items");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Response).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Resolution).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.ExpectedLocation).HasMaxLength(240);
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.Property(x => x.ResolutionNotes).HasMaxLength(1000);
            entity.Property(x => x.ResolvedBy).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.CampaignId });
            entity.HasIndex(x => new { x.OrganizationId, x.ParticipantId });
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId }).HasDatabaseName("IX_tenant_audit_items_asset");
            entity.HasIndex(x => new { x.OrganizationId, x.ExpectedPersonId }).HasDatabaseName("IX_tenant_audit_items_expected_person");
            entity.HasOne<AssetAuditCampaign>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.CampaignId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AssetAuditParticipant>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.ParticipantId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Asset>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.ExpectedPersonId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureReservations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipmentReservation>(entity =>
        {
            entity.ToTable("equipment_reservations");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
            entity.Property(x => x.PickupLocation).HasMaxLength(240);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.ApprovedBy).HasMaxLength(240);
            entity.Property(x => x.RejectedBy).HasMaxLength(240);
            entity.Property(x => x.DecisionNotes).HasMaxLength(2000);
            entity.Property(x => x.CancelledBy).HasMaxLength(240);
            entity.Property(x => x.CancellationReason).HasMaxLength(2000);
            // Token współbieżności wymagany przez sekcję 8.5 - zapobiega zatwierdzeniu dwóch nachodzących
            // rezerwacji tego samego aktywa w równoległych żądaniach.
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.OrganizationId, x.RequesterPersonId });
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.StartAt, x.EndAt });
            entity.HasIndex(x => new { x.OrganizationId, x.AssignmentId }).HasDatabaseName("IX_tenant_reservations_assignment");
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.RequesterPersonId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Assignment>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Items).WithOne()
                .HasForeignKey(x => new { x.OrganizationId, x.ReservationId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EquipmentReservationItem>(entity =>
        {
            entity.ToTable("equipment_reservation_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.SubstitutionReason).HasMaxLength(1000);
            entity.HasIndex(x => new { x.OrganizationId, x.ReservationId });
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId });
            entity.HasIndex(x => new { x.OrganizationId, x.RequestedCategoryId }).HasDatabaseName("IX_tenant_reservation_items_category");
            entity.HasIndex(x => new { x.OrganizationId, x.OriginalAssetId }).HasDatabaseName("IX_tenant_reservation_items_original_asset");
            entity.HasIndex(x => new { x.OrganizationId, x.KitDefinitionId }).HasDatabaseName("IX_tenant_reservation_items_kit");
            entity.HasOne<AssetCategory>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.RequestedCategoryId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Asset>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Asset>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.OriginalAssetId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOffboarding(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OffboardingCase>(entity =>
        {
            entity.ToTable("offboarding_cases");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.DefaultReturnLocation).HasMaxLength(240);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.PublicTokenHash).HasMaxLength(128);
            entity.Property(x => x.CreatedBy).HasMaxLength(240).IsRequired();
            entity.Property(x => x.CompletedBy).HasMaxLength(240);
            entity.Property(x => x.CancellationReason).HasMaxLength(1000);
            entity.Property(x => x.FinalProtocolNumber).HasMaxLength(80);
            // Tylko jedna niezakończona sprawa offboardingowa na osobę w organizacji (sekcja 4.3, 4.12).
            entity.HasIndex(x => new { x.OrganizationId, x.PersonId })
                .IsUnique()
                .HasFilter("\"Status\" NOT IN ('Completed', 'Cancelled')")
                .HasDatabaseName("IX_offboarding_cases_OrganizationId_PersonId_Open");
            entity.HasIndex(x => x.PublicTokenHash)
                .IsUnique()
                .HasFilter("\"PublicTokenHash\" IS NOT NULL")
                .HasDatabaseName("IX_offboarding_cases_PublicTokenHash");

            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.PersonId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);

            // P0-TENANT-005 (audyt 2026-08-17): ProcessOwnerId musi wskazywać osobę tej samej organizacji.
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.ProcessOwnerId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OffboardingItem>(entity =>
        {
            entity.ToTable("offboarding_items");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.AutomationMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(240).IsRequired();
            entity.Property(x => x.EmployeeResponse).HasMaxLength(60);
            entity.Property(x => x.EmployeeComment).HasMaxLength(1000);
            entity.Property(x => x.AutomationError).HasMaxLength(1000);
            entity.Property(x => x.ReceivedBy).HasMaxLength(240);
            entity.Property(x => x.InspectionCompletedBy).HasMaxLength(240);
            entity.Property(x => x.ResolutionNotes).HasMaxLength(1000);
            entity.Property(x => x.CompletedBy).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.OffboardingCaseId });
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId }).HasDatabaseName("IX_tenant_offboarding_items_asset");
            entity.HasIndex(x => new { x.OrganizationId, x.AssignmentId }).HasDatabaseName("IX_tenant_offboarding_items_assignment");
            entity.HasIndex(x => new { x.OrganizationId, x.LicenseId }).HasDatabaseName("IX_tenant_offboarding_items_license");
            entity.HasOne<OffboardingCase>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.OffboardingCaseId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Asset>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Assignment>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<License>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.LicenseId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAssetEvidence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetEvidence>(entity =>
        {
            entity.ToTable("asset_evidence");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Caption).HasMaxLength(500);
            entity.Property(x => x.UploadedBy).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Phase).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.UploadedVia).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId, x.Phase });
            entity.HasIndex(x => new { x.OrganizationId, x.OffboardingItemId }).HasDatabaseName("IX_tenant_evidence_offboarding_item");
            entity.HasIndex(x => new { x.OrganizationId, x.AssetAuditItemId }).HasDatabaseName("IX_tenant_evidence_audit_item");
            entity.HasOne<Asset>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Assignment>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<OffboardingItem>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.OffboardingItemId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AssetAuditItem>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetAuditItemId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDashboards(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DashboardLayout>(entity =>
        {
            entity.ToTable("dashboard_layouts");
            entity.HasKey(x => x.OrganizationUserId);
            entity.Property(x => x.LayoutJson).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.OrganizationUserId }).HasDatabaseName("IX_tenant_dashboard_layout_user");
            entity.HasOne<OrganizationUser>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.OrganizationUserId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DashboardSnapshot>(entity =>
        {
            entity.ToTable("dashboard_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VisibleAssetValue).HasColumnType("numeric(18,2)");
            entity.HasIndex(x => new { x.OrganizationId, x.SnapshotDate }).IsUnique();
        });
    }

    private static void ConfigureAlerts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentAlert>(entity =>
        {
            entity.ToTable("sent_alerts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AlertKey).HasMaxLength(60).IsRequired();
            entity.Property(x => x.RecipientEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(SentAlert.LastErrorMaxLength);
            entity.HasIndex(x => new { x.OrganizationId, x.AlertKey, x.EntityId, x.RecipientEmail }).IsUnique();
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.ToTable("alert_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.DeliveryMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.RecipientMode).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.CustomEmails).HasMaxLength(600);
            entity.Property(x => x.UpdatedBy).HasMaxLength(240).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Type }).IsUnique();
            entity.HasIndex(x => x.OrganizationId);
        });

        modelBuilder.Entity<AlertDigestSettings>(entity =>
        {
            entity.ToTable("alert_digest_settings");
            entity.HasKey(x => x.OrganizationId);
            entity.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.BusinessDays).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.HolidayCalendarCountryCode).HasMaxLength(8);
            entity.Property(x => x.IncludeEmptyDigest).IsRequired();
        });
    }

    private static void ConfigureOrganizations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Country).HasMaxLength(8).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(8).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(8).IsRequired();
            entity.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LogoUrl).HasMaxLength(600);
            entity.Property(x => x.CapturePublicIp).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.PrivacyNoticeUrl).HasMaxLength(600);
            entity.Property(x => x.PrivacyContactEmail).HasMaxLength(320);
            entity.Property(x => x.QrLabelShowName).HasDefaultValue(true);
            entity.Property(x => x.QrLabelShowTag).HasDefaultValue(true);
        });
    }

    private void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationUser>(entity =>
        {
            entity.ToTable("organization_users");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Email).HasMaxLength(240).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(400);
            entity.Property(x => x.SecurityStamp).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.PersonId })
                .IsUnique()
                .HasFilter("\"PersonId\" IS NOT NULL");
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.PersonId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);
            // Szyfrowane at-rest (audyt P1.4) - HasMaxLength podniesiony bo ciphertext (nonce+tag+base64) jest
            // dłuższy niż surowy base32 secret.
            entity.Property(x => x.TotpSecret)
                .HasMaxLength(200)
                .HasConversion(
                    plain => plain == null ? null : _fieldEncryptor.Encrypt(FieldEncryptionPurposes.TotpSecret, plain),
                    stored => stored == null ? null : _fieldEncryptor.Decrypt(FieldEncryptionPurposes.TotpSecret, stored));
            entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.OwnsMany(x => x.Roles, owned =>
            {
                owned.ToTable("organization_user_roles");
                owned.WithOwner().HasForeignKey(x => x.UserId);
                owned.HasKey(x => new { x.UserId, x.Role });
                owned.Property(x => x.Role).HasMaxLength(80).IsRequired();
            });
        });

        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            entity.ToTable("external_logins");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ProviderUserId).HasMaxLength(240).IsRequired();
            entity.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.OrganizationUserId);
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.ToTable("email_verification_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.OrganizationUserId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(120).IsRequired();
            entity.Property(x => x.FamilyId).IsRequired();
            entity.Property(x => x.RevocationReason).HasMaxLength(80);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.OrganizationUserId);
            entity.HasIndex(x => x.FamilyId);
            entity.HasIndex(x => x.ParentTokenId);
            entity.HasIndex(x => x.ReplacedByTokenId);
            entity.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ParentTokenId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OAuthTransaction>(entity =>
        {
            entity.ToTable("oauth_transactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StateHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CodeVerifier).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ReturnPath).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.CorrelationHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Nonce).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.StateHash).IsUnique();
            entity.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<TwoFactorChallenge>(entity =>
        {
            entity.ToTable("two_factor_challenges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TicketHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.TicketHash).IsUnique();
            entity.HasIndex(x => x.ExpiresAt);
            entity.HasIndex(x => x.OrganizationUserId);
            entity.HasOne<OrganizationUser>().WithMany()
                .HasForeignKey(x => x.OrganizationUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceTrustToken>(entity =>
        {
            entity.ToTable("device_trust_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.OrganizationUserId, x.TokenHash }).IsUnique();
        });

        modelBuilder.Entity<TwoFactorRecoveryCode>(entity =>
        {
            entity.ToTable("two_factor_recovery_codes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodeHash).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.OrganizationUserId);
        });
    }

    private static void ConfigureAssets(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetCategory>(entity =>
        {
            entity.ToTable("asset_categories");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(600);
            entity.Property(x => x.Icon).HasMaxLength(40);
            entity.Property(x => x.ReturnHandlingMode).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.PostReturnDisposition).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReturnChecklistTemplate).HasMaxLength(2000);
            entity.Property(x => x.PhotoOnIssue).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.PhotoOnReturn).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

            entity.OwnsMany(x => x.FieldDefinitions, owned =>
            {
                owned.ToTable("asset_field_definitions");
                owned.WithOwner().HasForeignKey(x => x.CategoryId);
                owned.HasKey(x => new { x.CategoryId, x.Id });
                owned.Property(x => x.Id).ValueGeneratedNever();
                owned.Property(x => x.Key).HasMaxLength(80).IsRequired();
                owned.Property(x => x.Label).HasMaxLength(120).IsRequired();
                owned.Property(x => x.FieldType).HasConversion<string>().HasMaxLength(40).IsRequired();
                owned.Property(x => x.Options).HasMaxLength(1000);
            });
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("assets");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.AssetTag).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SerialNumber).HasMaxLength(120);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(180);
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId });
            entity.HasOne<Location>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.LocationId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Manufacturer).HasMaxLength(120);
            entity.Property(x => x.Model).HasMaxLength(120);
            entity.Property(x => x.Currency).HasMaxLength(8);
            entity.Property(x => x.QrCodePayload).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.OrganizationId, x.AssetTag }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.HasIndex(x => new { x.OrganizationId, x.CategoryId }).HasDatabaseName("IX_tenant_assets_category");
            entity.HasOne<AssetCategory>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.CategoryId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            // Drugie linia obrony dla P0-TENANT-001/006 (audyt 2026-08-17): Application waliduje TeamId/
            // AssignedPersonId po (OrganizationId, Id) przed zapisem, DB odrzuci wszystko co to ominie.
            entity.HasOne<Team>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.TeamId })
                .HasPrincipalKey(t => new { t.OrganizationId, t.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssignedPersonId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.OwnsMany(x => x.FieldValues, owned =>
            {
                owned.ToTable("asset_field_values");
                owned.WithOwner().HasForeignKey(x => x.AssetId);
                owned.HasKey(x => new { x.AssetId, x.FieldKey });
                owned.Property(x => x.FieldKey).HasMaxLength(80).IsRequired();
                owned.Property(x => x.Value).HasColumnType("text").IsRequired();
            });
        });

        modelBuilder.Entity<AssetInspection>(entity =>
        {
            entity.ToTable("asset_inspections");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.CreatedBy).HasMaxLength(240);
            entity.Property(x => x.DamageAssessmentNotes).HasMaxLength(2000);
            entity.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.CompletedBy).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId, x.Outcome });
            entity.HasIndex(x => new { x.OrganizationId, x.AssignmentId }).HasDatabaseName("IX_tenant_inspections_assignment");
            entity.HasIndex(x => new { x.OrganizationId, x.OffboardingItemId }).HasDatabaseName("IX_tenant_inspections_offboarding_item");
            entity.HasOne<Asset>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Assignment>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OffboardingItem>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.OffboardingItemId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceTicket>(entity =>
        {
            entity.ToTable("service_tickets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Vendor).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Resolution).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId, x.Status });
            entity.HasOne<Asset>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            // P0-TENANT-007 (audyt 2026-08-17): AssetInspectionId musi wskazywać inspekcję tej samej organizacji.
            entity.HasOne<AssetInspection>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.AssetInspectionId })
                .HasPrincipalKey(i => new { i.OrganizationId, i.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLocations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("asset_locations");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.ParentId });
            entity.HasIndex(x => new { x.OrganizationId, x.ParentId, x.NormalizedName }).IsUnique().HasFilter("\"ParentId\" IS NOT NULL");
            entity.HasIndex(x => new { x.OrganizationId, x.NormalizedName }).IsUnique().HasFilter("\"ParentId\" IS NULL");
            entity.HasOne<Location>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.ParentId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePeople(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("people", table => table.HasCheckConstraint(
                "CK_people_employment_status_active",
                "(\"EmploymentStatus\" IN ('Active', 'Offboarding') AND \"IsActive\") OR (\"EmploymentStatus\" = 'Inactive' AND NOT \"IsActive\")"));
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.EmployeeNumber).HasMaxLength(80);
            entity.Property(x => x.RelationType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.JobTitle).HasMaxLength(120);
            entity.Property(x => x.Location).HasMaxLength(180);
            entity.HasIndex(x => new { x.OrganizationId, x.LocationId });
            entity.HasOne<Location>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.LocationId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.CostCenter).HasMaxLength(80);
            entity.Property(x => x.EmploymentStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.PreferredLanguage).HasMaxLength(8);
            entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.EmploymentStatus });
            entity.HasIndex(x => new { x.OrganizationId, x.EmploymentEndsAt });

            // P0-TENANT-002 (audyt 2026-08-17): TeamId/ManagerId muszą wskazywać zasoby tej samej organizacji.
            entity.HasOne<Team>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.TeamId })
                .HasPrincipalKey(t => new { t.OrganizationId, t.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.ManagerId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CostCenter).HasMaxLength(80);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

            // P0-TENANT-003 (audyt 2026-08-17): ManagerId musi wskazywać osobę tej samej organizacji.
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.ManagerId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PersonRelationType>(entity =>
        {
            entity.ToTable("person_relation_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        });
    }

    private static void ConfigureProcedures(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Procedure>(entity =>
        {
            entity.ToTable("procedures");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Title).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Version).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Owner).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.AppliesTo).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.Title, x.Version });
        });

        modelBuilder.Entity<ProcedureDocument>(entity =>
        {
            entity.ToTable("procedure_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.UploadedBy).HasMaxLength(240).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.ProcedureId, x.UploadedAt });
            entity.HasOne<Procedure>().WithMany(x => x.Documents)
                .HasForeignKey(x => new { x.OrganizationId, x.ProcedureId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureJobProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobProfile>(entity =>
        {
            entity.ToTable("job_profiles");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(140).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(800);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.OwnsMany(x => x.AssetCategories, owned =>
            {
                owned.ToTable("job_profile_asset_categories");
                owned.WithOwner()
                    .HasForeignKey(x => new { x.OrganizationId, x.JobProfileId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
                owned.HasKey(x => new { x.JobProfileId, x.AssetCategoryId });
                owned.HasIndex(x => new { x.OrganizationId, x.JobProfileId }).HasDatabaseName("IX_tenant_jobprofile_categories_owner");
                owned.HasIndex(x => new { x.OrganizationId, x.AssetCategoryId }).HasDatabaseName("IX_tenant_jobprofile_categories_category");
                owned.HasOne<AssetCategory>().WithMany()
                    .HasForeignKey(x => new { x.OrganizationId, x.AssetCategoryId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                    .OnDelete(DeleteBehavior.Restrict);
            });
            entity.OwnsMany(x => x.Procedures, owned =>
            {
                owned.ToTable("job_profile_procedures");
                owned.WithOwner()
                    .HasForeignKey(x => new { x.OrganizationId, x.JobProfileId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
                owned.HasKey(x => new { x.JobProfileId, x.ProcedureId });
                owned.HasIndex(x => new { x.OrganizationId, x.JobProfileId }).HasDatabaseName("IX_tenant_jobprofile_procedures_owner");
                owned.HasIndex(x => new { x.OrganizationId, x.ProcedureId }).HasDatabaseName("IX_tenant_jobprofile_procedures_procedure");
                owned.HasOne<Procedure>().WithMany()
                    .HasForeignKey(x => new { x.OrganizationId, x.ProcedureId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // P0-TENANT-004 (audyt 2026-08-17): DefaultManagerId musi wskazywać osobę tej samej organizacji.
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.DefaultManagerId })
                .HasPrincipalKey(p => new { p.OrganizationId, p.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetStatusSetting>(entity =>
        {
            entity.ToTable("asset_status_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StatusKey).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Color).HasMaxLength(9).IsRequired();
            entity.Property(x => x.BackgroundColor).HasMaxLength(9).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.StatusKey }).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RoleKey).HasMaxLength(60).IsRequired();
            entity.Property(x => x.PermissionKey).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.RoleKey, x.PermissionKey }).IsUnique();
        });
    }

    private void ConfigureLicenses(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<License>(entity =>
        {
            entity.ToTable("licenses");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(160);
            // Ciphertext includes nonce/tag/key id/base64 overhead; PostgreSQL text avoids truncating a valid encrypted plaintext.
            entity.Property(x => x.LicenseKey)
                .HasColumnType("text")
                .HasConversion(
                    plain => plain == null ? null : _fieldEncryptor.Encrypt(FieldEncryptionPurposes.LicenseKey, plain),
                    stored => stored == null ? null : _fieldEncryptor.Decrypt(FieldEncryptionPurposes.LicenseKey, stored));
            entity.Property(x => x.Notes).HasMaxLength(800);

            entity.OwnsMany(x => x.Seats, owned =>
            {
                owned.ToTable("license_seats");
                owned.WithOwner()
                    .HasForeignKey(x => new { x.OrganizationId, x.LicenseId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
                owned.HasKey(x => new { x.LicenseId, x.PersonId });
                owned.HasIndex(x => new { x.OrganizationId, x.LicenseId }).HasDatabaseName("IX_tenant_license_seats_owner");
                owned.HasIndex(x => new { x.OrganizationId, x.PersonId }).HasDatabaseName("IX_tenant_license_seats_person");
                owned.HasOne<Person>().WithMany()
                    .HasForeignKey(x => new { x.OrganizationId, x.PersonId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                    .OnDelete(DeleteBehavior.Restrict);
            });
        });
    }

    private static void ConfigureAssignments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("assignments");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.OrganizationId, x.Id });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(800);
            entity.Property(x => x.ProtocolNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(240).IsRequired();
            entity.Property(x => x.PublicTokenHash).HasMaxLength(128);
            entity.HasIndex(x => new { x.OrganizationId, x.ProtocolNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.PersonId }).HasDatabaseName("IX_tenant_assignments_person");
            entity.HasIndex(x => x.PublicTokenHash)
                .IsUnique()
                .HasFilter("\"PublicTokenHash\" IS NOT NULL")
                .HasDatabaseName("IX_assignments_PublicTokenHash");
            entity.HasOne<Person>().WithMany()
                .HasForeignKey(x => new { x.OrganizationId, x.PersonId })
                .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.OwnsMany(x => x.Assets, owned =>
            {
                owned.ToTable("assignment_assets");
                owned.WithOwner()
                    .HasForeignKey(x => new { x.OrganizationId, x.AssignmentId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
                owned.HasKey(x => new { x.AssignmentId, x.AssetId });
                owned.HasIndex(x => new { x.OrganizationId, x.AssignmentId }).HasDatabaseName("IX_tenant_assignment_assets_owner");
                owned.HasIndex(x => new { x.OrganizationId, x.AssetId }).HasDatabaseName("IX_tenant_assignment_assets_asset");
                owned.HasOne<Asset>().WithMany()
                    .HasForeignKey(x => new { x.OrganizationId, x.AssetId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                    .OnDelete(DeleteBehavior.Restrict);
                owned.Property(x => x.IssueCondition).HasMaxLength(400).IsRequired();
                owned.Property(x => x.ReturnCondition).HasMaxLength(400);
                owned.Property(x => x.ReturnResolution).HasConversion<string>().HasMaxLength(40);
                owned.Property(x => x.ReturnLocation).HasMaxLength(200);
                owned.Property(x => x.ReturnedBy).HasMaxLength(240);
                owned.Property(x => x.ReturnNotes).HasMaxLength(800);
            });

            entity.OwnsMany(x => x.ProcedureAcceptances, owned =>
            {
                owned.ToTable("procedure_acceptances");
                owned.WithOwner()
                    .HasForeignKey(x => new { x.OrganizationId, x.AssignmentId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
                owned.HasKey(x => x.Id);
                owned.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
                owned.HasIndex(x => new { x.OrganizationId, x.AssignmentId }).HasDatabaseName("IX_tenant_procedure_acceptances_owner");
                owned.HasIndex(x => new { x.OrganizationId, x.ProcedureId }).HasDatabaseName("IX_tenant_procedure_acceptances_procedure");
                owned.HasIndex(x => new { x.OrganizationId, x.PersonId }).HasDatabaseName("IX_tenant_procedure_acceptances_person");
                owned.HasOne<Procedure>().WithMany()
                    .HasForeignKey(x => new { x.OrganizationId, x.ProcedureId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                    .OnDelete(DeleteBehavior.Restrict);
                owned.HasOne<Person>().WithMany()
                    .HasForeignKey(x => new { x.OrganizationId, x.PersonId })
                    .HasPrincipalKey(x => new { x.OrganizationId, x.Id })
                    .OnDelete(DeleteBehavior.Restrict);
            });
        });
    }

    private static void ConfigureActivity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("activity_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ActorSubject).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(1000);
            entity.Property(x => x.SourceIp).HasMaxLength(64);
            entity.HasIndex(x => x.SourceIpExpiresAt);
            entity.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        });
    }

    private static void ConfigureSubscriptions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationSubscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlanKey).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.StripeCustomerId).HasMaxLength(80);
            entity.Property(x => x.StripeSubscriptionId).HasMaxLength(80);
            entity.HasIndex(x => x.OrganizationId).IsUnique();
            entity.HasIndex(x => x.StripeCustomerId);
        });

        modelBuilder.Entity<ProcessedStripeEvent>(entity =>
        {
            entity.ToTable("processed_stripe_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventId).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.EventId).IsUnique();
        });
    }
}

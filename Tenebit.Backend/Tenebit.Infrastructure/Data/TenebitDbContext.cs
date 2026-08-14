using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Audits;
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
    public TenebitDbContext(DbContextOptions<TenebitDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetInspection> AssetInspections => Set<AssetInspection>();
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
    public DbSet<DeviceTrustToken> DeviceTrustTokens => Set<DeviceTrustToken>();
    public DbSet<TwoFactorRecoveryCode> TwoFactorRecoveryCodes => Set<TwoFactorRecoveryCode>();
    public DbSet<AssetStatusSetting> AssetStatusSettings => Set<AssetStatusSetting>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<OrganizationSubscription> Subscriptions => Set<OrganizationSubscription>();
    public DbSet<SentAlert> SentAlerts => Set<SentAlert>();
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
    public DbSet<EquipmentKitDefinition> EquipmentKitDefinitions => Set<EquipmentKitDefinition>();
    public DbSet<EquipmentKitDefinitionItem> EquipmentKitDefinitionItems => Set<EquipmentKitDefinitionItem>();
    public DbSet<EquipmentReservation> EquipmentReservations => Set<EquipmentReservation>();
    public DbSet<EquipmentReservationItem> EquipmentReservationItems => Set<EquipmentReservationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenebit");
        ConfigureOrganizations(modelBuilder);
        ConfigureIdentity(modelBuilder);
        ConfigureAssets(modelBuilder);
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
    }

    private static void ConfigureAudits(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetAuditCampaign>(entity =>
        {
            entity.ToTable("asset_audit_campaigns");
            entity.HasKey(x => x.Id);
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
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.HasIndex(x => new { x.OrganizationId, x.CampaignId, x.PersonId }).IsUnique();
            entity.HasOne<AssetAuditCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetAuditItem>(entity =>
        {
            entity.ToTable("asset_audit_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Response).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Resolution).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.ExpectedLocation).HasMaxLength(240);
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.Property(x => x.ResolutionNotes).HasMaxLength(1000);
            entity.Property(x => x.ResolvedBy).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.CampaignId });
            entity.HasIndex(x => new { x.OrganizationId, x.ParticipantId });
            entity.HasOne<AssetAuditCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AssetAuditParticipant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureReservations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipmentKitDefinition>(entity =>
        {
            entity.ToTable("equipment_kit_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.CreatedBy).HasMaxLength(240).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.KitDefinitionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EquipmentKitDefinitionItem>(entity =>
        {
            entity.ToTable("equipment_kit_definition_items");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.OrganizationId, x.KitDefinitionId });
        });

        modelBuilder.Entity<EquipmentReservation>(entity =>
        {
            entity.ToTable("equipment_reservations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
            entity.Property(x => x.PickupLocation).HasMaxLength(240);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.ApprovedBy).HasMaxLength(240);
            entity.Property(x => x.RejectedBy).HasMaxLength(240);
            entity.Property(x => x.DecisionNotes).HasMaxLength(2000);
            entity.Property(x => x.CancelledBy).HasMaxLength(240);
            entity.Property(x => x.CancellationReason).HasMaxLength(2000);
            // Token współbieżności wymagany przez sekcję 8.5 — zapobiega zatwierdzeniu dwóch nachodzących
            // rezerwacji tego samego aktywa w równoległych żądaniach.
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.OrganizationId, x.RequesterPersonId });
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.StartAt, x.EndAt });
            entity.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EquipmentReservationItem>(entity =>
        {
            entity.ToTable("equipment_reservation_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.SubstitutionReason).HasMaxLength(1000);
            entity.HasIndex(x => new { x.OrganizationId, x.ReservationId });
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId });
        });
    }

    private static void ConfigureOffboarding(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OffboardingCase>(entity =>
        {
            entity.ToTable("offboarding_cases");
            entity.HasKey(x => x.Id);
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
        });

        modelBuilder.Entity<OffboardingItem>(entity =>
        {
            entity.ToTable("offboarding_items");
            entity.HasKey(x => x.Id);
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
            entity.HasOne<OffboardingCase>().WithMany().HasForeignKey(x => x.OffboardingCaseId).OnDelete(DeleteBehavior.Cascade);
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
            entity.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureDashboards(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DashboardLayout>(entity =>
        {
            entity.ToTable("dashboard_layouts");
            entity.HasKey(x => x.OrganizationUserId);
            entity.Property(x => x.LayoutJson).IsRequired();
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
        });
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationUser>(entity =>
        {
            entity.ToTable("organization_users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(240).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(400);
            entity.Property(x => x.TotpSecret).HasMaxLength(64);
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
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.OrganizationUserId);
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
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(600);
            entity.Property(x => x.Icon).HasMaxLength(40);
            entity.Property(x => x.ReturnHandlingMode).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.PostReturnDisposition).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReturnChecklistTemplate).HasMaxLength(2000);
            entity.Property(x => x.PhotoOnIssue).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.PhotoOnReturn).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.CatalogName).HasMaxLength(120);
            entity.Property(x => x.CatalogDescription).HasMaxLength(600);
            entity.Property(x => x.CatalogImageUrl).HasMaxLength(600);
            entity.Property(x => x.ReservationMode).HasConversion<string>().HasMaxLength(40).IsRequired();
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
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.AssetTag).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SerialNumber).HasMaxLength(120);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(180);
            entity.Property(x => x.Manufacturer).HasMaxLength(120);
            entity.Property(x => x.Model).HasMaxLength(120);
            entity.Property(x => x.Currency).HasMaxLength(8);
            entity.Property(x => x.QrCodePayload).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            entity.Property(x => x.ReservationInstructions).HasMaxLength(2000);
            entity.HasIndex(x => new { x.OrganizationId, x.AssetTag }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Status });

            entity.OwnsMany(x => x.FieldValues, owned =>
            {
                owned.ToTable("asset_field_values");
                owned.WithOwner().HasForeignKey(x => x.AssetId);
                owned.HasKey(x => new { x.AssetId, x.FieldKey });
                owned.Property(x => x.FieldKey).HasMaxLength(80).IsRequired();
                owned.Property(x => x.Value).HasMaxLength(2000).IsRequired();
            });
        });

        modelBuilder.Entity<AssetInspection>(entity =>
        {
            entity.ToTable("asset_inspections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedBy).HasMaxLength(240);
            entity.Property(x => x.DamageAssessmentNotes).HasMaxLength(2000);
            entity.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.CompletedBy).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.AssetId, x.Outcome });
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
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.EmployeeNumber).HasMaxLength(80);
            entity.Property(x => x.RelationType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.JobTitle).HasMaxLength(120);
            entity.Property(x => x.Location).HasMaxLength(180);
            entity.Property(x => x.CostCenter).HasMaxLength(80);
            entity.Property(x => x.EmploymentStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.PreferredLanguage).HasMaxLength(8);
            entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.EmploymentStatus });
            entity.HasIndex(x => new { x.OrganizationId, x.EmploymentEndsAt });
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CostCenter).HasMaxLength(80);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
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
            entity.Property(x => x.Title).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Version).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Owner).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.AppliesTo).HasMaxLength(240);
            entity.HasIndex(x => new { x.OrganizationId, x.Title, x.Version });
            entity.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.ProcedureId).OnDelete(DeleteBehavior.Cascade);
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
        });
    }

    private static void ConfigureJobProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobProfile>(entity =>
        {
            entity.ToTable("job_profiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(140).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(800);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.OwnsMany(x => x.AssetCategories, owned =>
            {
                owned.ToTable("job_profile_asset_categories");
                owned.WithOwner().HasForeignKey(x => x.JobProfileId);
                owned.HasKey(x => new { x.JobProfileId, x.AssetCategoryId });
            });
            entity.OwnsMany(x => x.Procedures, owned =>
            {
                owned.ToTable("job_profile_procedures");
                owned.WithOwner().HasForeignKey(x => x.JobProfileId);
                owned.HasKey(x => new { x.JobProfileId, x.ProcedureId });
            });
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

    private static void ConfigureLicenses(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<License>(entity =>
        {
            entity.ToTable("licenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(160);
            entity.Property(x => x.LicenseKey).HasMaxLength(400);
            entity.Property(x => x.Notes).HasMaxLength(800);

            entity.OwnsMany(x => x.Seats, owned =>
            {
                owned.ToTable("license_seats");
                owned.WithOwner().HasForeignKey("LicenseId");
                owned.HasKey(x => new { x.LicenseId, x.PersonId });
            });
        });
    }

    private static void ConfigureAssignments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(800);
            entity.Property(x => x.ProtocolNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedBy).HasMaxLength(240).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.ProtocolNumber }).IsUnique();

            entity.OwnsMany(x => x.Assets, owned =>
            {
                owned.ToTable("assignment_assets");
                owned.WithOwner().HasForeignKey("AssignmentId");
                owned.HasKey(x => new { x.AssignmentId, x.AssetId });
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
                owned.WithOwner().HasForeignKey("AssignmentId");
                owned.HasKey(x => x.Id);
                owned.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
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
    }
}

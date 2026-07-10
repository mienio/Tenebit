using Microsoft.EntityFrameworkCore;
using Tenebit.Application.Abstractions;
using Tenebit.Domain.Alerts;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audit;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Organizations;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Settings;
using Tenebit.Domain.Subscriptions;

namespace Tenebit.Infrastructure.Data;

public sealed class TenebitDbContext : DbContext, IUnitOfWork
{
    public TenebitDbContext(DbContextOptions<TenebitDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Procedure> Procedures => Set<Procedure>();
    public DbSet<ProcedureDocument> ProcedureDocuments => Set<ProcedureDocument>();
    public DbSet<JobProfile> JobProfiles => Set<JobProfile>();
    public DbSet<OrganizationUser> OrganizationUsers => Set<OrganizationUser>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AssetStatusSetting> AssetStatusSettings => Set<AssetStatusSetting>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<OrganizationSubscription> Subscriptions => Set<OrganizationSubscription>();
    public DbSet<SentAlert> SentAlerts => Set<SentAlert>();

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
    }

    private static void ConfigureAlerts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SentAlert>(entity =>
        {
            entity.ToTable("sent_alerts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AlertKey).HasMaxLength(60).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.AlertKey, x.EntityId }).IsUnique();
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
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();

            entity.OwnsMany(x => x.FieldDefinitions, owned =>
            {
                owned.ToTable("asset_field_definitions");
                owned.WithOwner().HasForeignKey(x => x.CategoryId);
                owned.HasKey(x => new { x.CategoryId, x.Id });
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
    }

    private static void ConfigurePeople(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("people");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.EmployeeNumber).HasMaxLength(80);
            entity.Property(x => x.RelationType).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.JobTitle).HasMaxLength(120);
            entity.Property(x => x.Location).HasMaxLength(180);
            entity.Property(x => x.CostCenter).HasMaxLength(80);
            entity.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CostCenter).HasMaxLength(80);
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
            entity.HasIndex(x => new { x.OrganizationId, x.StatusKey }).IsUnique();
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
            entity.HasIndex(x => x.OrganizationId).IsUnique();
        });
    }
}

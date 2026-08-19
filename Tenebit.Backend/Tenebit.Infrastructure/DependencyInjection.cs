using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tenebit.Application.Abstractions;
using Tenebit.Infrastructure.Data;
using Tenebit.Infrastructure.Repositories;
using Tenebit.Infrastructure.Seed;
using Tenebit.Infrastructure.Services;

namespace Tenebit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TenebitDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Brakuje ConnectionStrings:TenebitDb w konfiguracji.");
        }

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = configuration.GetValue("Database:MaxPoolSize", 40),
            MinPoolSize = configuration.GetValue("Database:MinPoolSize", 0)
        };
        if (configuration.GetValue("Database:RequireSsl", false))
        {
            connectionBuilder.SslMode = SslMode.Require;
        }

        var commandTimeoutSeconds = configuration.GetValue("Database:CommandTimeoutSeconds", 30);
        services.AddDbContext<TenebitDbContext>(options =>
            options.UseNpgsql(connectionBuilder.ConnectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
                npgsql.CommandTimeout(commandTimeoutSeconds);
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            }));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TenebitDbContext>());
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IAssetCategoryRepository, AssetCategoryRepository>();
        services.AddScoped<IAssetInspectionRepository, AssetInspectionRepository>();
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPersonRelationTypeRepository, PersonRelationTypeRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IProcedureRepository, ProcedureRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IOrganizationUserRepository, OrganizationUserRepository>();
        services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IDeviceTrustTokenRepository, DeviceTrustTokenRepository>();
        services.AddScoped<ITwoFactorRecoveryCodeRepository, TwoFactorRecoveryCodeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IOAuthTransactionRepository, OAuthTransactionRepository>();
        services.AddScoped<ITwoFactorChallengeRepository, TwoFactorChallengeRepository>();
        services.AddScoped<IJobProfileRepository, JobProfileRepository>();
        services.AddScoped<IAssetStatusSettingRepository, AssetStatusSettingRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IProcessedStripeEventRepository, ProcessedStripeEventRepository>();
        services.AddScoped<ISentAlertRepository, SentAlertRepository>();
        services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
        services.AddScoped<IAlertDigestSettingsRepository, AlertDigestSettingsRepository>();
        services.AddScoped<IDashboardReadRepository, DashboardReadRepository>();
        services.AddScoped<IDashboardLayoutRepository, DashboardLayoutRepository>();
        services.AddScoped<IDashboardSnapshotRepository, DashboardSnapshotRepository>();
        services.AddScoped<IAssetEvidenceRepository, AssetEvidenceRepository>();
        services.AddScoped<IOffboardingCaseRepository, OffboardingCaseRepository>();
        services.AddScoped<IOffboardingItemRepository, OffboardingItemRepository>();
        services.AddScoped<IAssetAuditCampaignRepository, AssetAuditCampaignRepository>();
        services.AddScoped<IAssetAuditParticipantRepository, AssetAuditParticipantRepository>();
        services.AddScoped<IAssetAuditItemRepository, AssetAuditItemRepository>();
        services.AddScoped<IEquipmentReservationRepository, EquipmentReservationRepository>();
        services.AddScoped<IServiceTicketRepository, ServiceTicketRepository>();
        services.AddSingleton<IUserSecurityStateCache, UserSecurityStateCache>();
        services.AddScoped<IDatabaseHealthProbe, DatabaseHealthProbe>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddSingleton<IImageSanitizer, ImageSanitizer>();
        services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailAvailability, EmailAvailability>();
        services.AddScoped<IEmailOutboxWriter, PostgresEmailOutboxWriter>();
        services.AddSingleton<IAppLinkBuilder, AppLinkBuilder>();
        services.AddHttpClient<IPaymentGateway, StripePaymentGateway>(client =>
        {
            client.BaseAddress = new Uri("https://api.stripe.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<IFieldEncryptor, FieldEncryptor>();
        services.AddSingleton<IPublicCapabilitySessionProtector, PublicCapabilitySessionProtector>();
        services.AddScoped<DefaultDataSeeder>();
        services.AddScoped<PostgresJobLock>();
        services.AddScoped<IAuthenticationAbuseLimiter, PostgresAuthenticationAbuseLimiter>();
        services.AddHostedService<AlertBackgroundService>();
        services.AddHostedService<DashboardSnapshotBackgroundService>();
        services.AddHostedService<OffboardingBackgroundService>();
        services.AddHostedService<EvidenceRetentionBackgroundService>();
        services.AddHostedService<ActivityLogRetentionBackgroundService>();
        services.AddHostedService<SecurityStateCleanupBackgroundService>();
        services.AddHostedService<PublicIpRetentionBackgroundService>();
        services.AddHostedService<SubscriptionReconciliationBackgroundService>();
        services.AddHostedService<EmailOutboxBackgroundService>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool forceMigrate = false, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        if (forceMigrate || configuration.GetValue("Database:AutoCreate", true))
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (configuration.GetValue("Seed:Enabled", true))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DefaultDataSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
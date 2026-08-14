using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddDbContext<TenebitDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TenebitDbContext>());
        services.AddScoped<IAssetRepository, AssetRepository>();
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
        services.AddScoped<IJobProfileRepository, JobProfileRepository>();
        services.AddScoped<IAssetStatusSettingRepository, AssetStatusSettingRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISentAlertRepository, SentAlertRepository>();
        services.AddScoped<IDashboardLayoutRepository, DashboardLayoutRepository>();
        services.AddScoped<IDashboardSnapshotRepository, DashboardSnapshotRepository>();
        services.AddScoped<IAssetEvidenceRepository, AssetEvidenceRepository>();
        services.AddScoped<IOffboardingCaseRepository, OffboardingCaseRepository>();
        services.AddScoped<IOffboardingItemRepository, OffboardingItemRepository>();
        services.AddScoped<IAssetAuditCampaignRepository, AssetAuditCampaignRepository>();
        services.AddScoped<IAssetAuditParticipantRepository, AssetAuditParticipantRepository>();
        services.AddScoped<IAssetAuditItemRepository, AssetAuditItemRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddSingleton<IPdfProtocolGenerator, PdfProtocolGenerator>();
        services.AddSingleton<IImageSanitizer, ImageSanitizer>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IAppLinkBuilder, AppLinkBuilder>();
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<DefaultDataSeeder>();
        services.AddHostedService<AlertBackgroundService>();
        services.AddHostedService<DashboardSnapshotBackgroundService>();
        services.AddHostedService<OffboardingBackgroundService>();
        services.AddHostedService<EvidenceRetentionBackgroundService>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        if (configuration.GetValue("Database:AutoCreate", true))
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
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
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IProcedureRepository, ProcedureRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IOrganizationUserRepository, OrganizationUserRepository>();
        services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
        services.AddScoped<IJobProfileRepository, JobProfileRepository>();
        services.AddScoped<IAssetStatusSettingRepository, AssetStatusSettingRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISentAlertRepository, SentAlertRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        services.AddSingleton<IPdfProtocolGenerator, PdfProtocolGenerator>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IAppLinkBuilder, AppLinkBuilder>();
        services.AddScoped<DefaultDataSeeder>();
        services.AddHostedService<AlertBackgroundService>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<TenebitDbContext>();
        if (configuration.GetValue("Database:AutoCreate", true))
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        await TenebitSchemaPatch.ApplyAsync(db, cancellationToken);
        await EnsureRuntimeSchemaAsync(db, cancellationToken);

        if (configuration.GetValue("Seed:Enabled", true))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DefaultDataSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
    }

    private static async Task EnsureRuntimeSchemaAsync(TenebitDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS tenebit;

            CREATE TABLE IF NOT EXISTS tenebit.asset_locations (
                "Id" uuid PRIMARY KEY,
                "OrganizationId" uuid NOT NULL,
                "Name" character varying(120) NOT NULL,
                "Type" character varying(40) NOT NULL,
                "ParentId" uuid NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL
            );

            CREATE INDEX IF NOT EXISTS "IX_asset_locations_OrganizationId_ParentId"
                ON tenebit.asset_locations ("OrganizationId", "ParentId");

            ALTER TABLE tenebit.procedures ADD COLUMN IF NOT EXISTS "ContentUrl" character varying(600) NULL;
            ALTER TABLE tenebit.procedures ADD COLUMN IF NOT EXISTS "AppliesTo" character varying(240) NULL;
            ALTER TABLE tenebit.procedures ADD COLUMN IF NOT EXISTS "ReviewDate" date NULL;
            ALTER TABLE tenebit.procedures ADD COLUMN IF NOT EXISTS "PublishedAt" timestamp with time zone NULL;

            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "Location" character varying(180) NULL;
            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "Manufacturer" character varying(120) NULL;
            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "Model" character varying(120) NULL;
            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "PurchasePrice" numeric(18,2) NULL;
            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "Currency" character varying(8) NULL;
            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "PurchaseDate" date NULL;
            ALTER TABLE tenebit.assets ADD COLUMN IF NOT EXISTS "WarrantyUntil" date NULL;

            ALTER TABLE tenebit.people ADD COLUMN IF NOT EXISTS "ManagerId" uuid NULL;
            ALTER TABLE tenebit.people ADD COLUMN IF NOT EXISTS "Location" character varying(180) NULL;
            ALTER TABLE tenebit.people ADD COLUMN IF NOT EXISTS "CostCenter" character varying(80) NULL;
            ALTER TABLE tenebit.people ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;

            CREATE TABLE IF NOT EXISTS tenebit.asset_field_definitions (
                "CategoryId" uuid NOT NULL,
                "Id" uuid NOT NULL,
                "Key" character varying(80) NOT NULL,
                "Label" character varying(120) NOT NULL,
                "FieldType" character varying(40) NOT NULL,
                "Options" character varying(1000) NULL,
                "Required" boolean NOT NULL DEFAULT FALSE,
                "SortOrder" integer NOT NULL DEFAULT 0,
                PRIMARY KEY ("CategoryId", "Id")
            );

            CREATE TABLE IF NOT EXISTS tenebit.asset_field_values (
                "AssetId" uuid NOT NULL,
                "FieldKey" character varying(80) NOT NULL,
                "Value" character varying(2000) NOT NULL,
                PRIMARY KEY ("AssetId", "FieldKey")
            );

            CREATE TABLE IF NOT EXISTS tenebit.sent_alerts (
                "Id" uuid PRIMARY KEY,
                "OrganizationId" uuid NOT NULL,
                "AlertKey" character varying(60) NOT NULL,
                "EntityId" uuid NOT NULL,
                "SentAt" timestamp with time zone NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_sent_alerts_Org_Key_Entity"
                ON tenebit.sent_alerts ("OrganizationId", "AlertKey", "EntityId");
            """, cancellationToken);
    }

}
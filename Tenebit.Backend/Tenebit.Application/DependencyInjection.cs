using Microsoft.Extensions.DependencyInjection;
using Tenebit.Application.Alerts;
using Tenebit.Application.Assets;
using Tenebit.Application.Audit;
using Tenebit.Application.Assignments;
using Tenebit.Application.Dashboard;
using Tenebit.Application.Evidence;
using Tenebit.Application.Onboarding;
using Tenebit.Application.JobProfiles;
using Tenebit.Application.Identity;
using Tenebit.Application.Licenses;
using Tenebit.Application.Settings;
using Tenebit.Application.Organizations;
using Tenebit.Application.People;
using Tenebit.Application.Procedures;
using Tenebit.Application.Subscriptions;
using Tenebit.Application.Workspace;

namespace Tenebit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AssetService>();
        services.AddScoped<AssetCategoryService>();
        services.AddScoped<AssetInspectionService>();
        services.AddScoped<PeopleService>();
        services.AddScoped<PersonOffboardingSchedulerService>();
        services.AddScoped<TeamService>();
        services.AddScoped<PersonRelationTypeService>();
        services.AddScoped<LicenseService>();
        services.AddScoped<RolePermissionService>();
        services.AddScoped<ProcedureService>();
        services.AddScoped<AssignmentService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<DashboardSnapshotService>();
        services.AddScoped<OnboardingService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<JobProfileService>();
        services.AddScoped<UserAccessService>();
        services.AddScoped<AuthService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<MyWorkspaceService>();
        services.AddScoped<AlertCheckService>();
        services.AddScoped<ActivityLogService>();
        services.AddScoped<AssetEvidenceService>();
        services.AddScoped<EvidenceRetentionService>();
        return services;
    }
}

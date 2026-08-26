using Microsoft.Extensions.DependencyInjection;
using Tenebit.Application.Admin;
using Tenebit.Application.Alerts;
using Tenebit.Application.Assets;
using Tenebit.Application.Audit;
using Tenebit.Application.Audits;
using Tenebit.Application.Assignments;
using Tenebit.Application.Dashboard;
using Tenebit.Application.Evidence;
using Tenebit.Application.Onboarding;
using Tenebit.Application.JobProfiles;
using Tenebit.Application.Identity;
using Tenebit.Application.Licenses;
using Tenebit.Application.Search;
using Tenebit.Application.Settings;
using Tenebit.Application.Offboarding;
using Tenebit.Application.Organizations;
using Tenebit.Application.People;
using Tenebit.Application.Procedures;
using Tenebit.Application.Reservations;
using Tenebit.Application.Subscriptions;
using Tenebit.Application.Workspace;

namespace Tenebit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Common.ManagerScopeService>();
        services.AddScoped<GlobalSearchService>();
        services.AddScoped<AdminOverviewService>();
        services.AddScoped<AdminModerationService>();
        services.AddScoped<AssetAuthorizationService>();
        services.AddScoped<AssetService>();
        services.AddScoped<LocationService>();
        services.AddScoped<LocationReferenceResolver>();
        services.AddScoped<AssetCategoryService>();
        services.AddScoped<AssetInspectionService>();
        services.AddScoped<MaintenanceService>();
        services.AddScoped<AssetReturnDispositionService>();
        services.AddScoped<PeopleService>();
        services.AddScoped<PersonOffboardingSchedulerService>();
        services.AddScoped<TeamService>();
        services.AddScoped<PersonRelationTypeService>();
        services.AddScoped<LicenseService>();
        services.AddScoped<RolePermissionService>();
        services.AddScoped<ProcedureService>();
        services.AddScoped<AssignmentResponseBuilder>();
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
        services.AddScoped<SubscriptionReconciliationService>();
        services.AddScoped<MyWorkspaceService>();
        services.AddScoped<AlertCheckService>();
        services.AddScoped<AlertSettingsService>();
        services.AddScoped<ActivityLogService>();
        services.AddScoped<ActivityLogRetentionService>();
        services.AddScoped<AssetEvidenceService>();
        services.AddScoped<EvidenceRetentionService>();
        services.AddScoped<OffboardingScheduledActionsService>();
        services.AddScoped<OffboardingResponseBuilder>();
        services.AddScoped<OffboardingService>();
        services.AddScoped<Protocols.ProtocolPdfService>();
        services.AddScoped<AssetAuditCampaignService>();
        services.AddScoped<ServiceTicketService>();
        return services;
    }
}

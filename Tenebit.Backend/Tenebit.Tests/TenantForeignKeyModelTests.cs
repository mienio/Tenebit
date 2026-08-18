using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Tenebit.Domain.Assets;
using Tenebit.Domain.Assignments;
using Tenebit.Domain.Audits;
using Tenebit.Domain.Dashboards;
using Tenebit.Domain.Evidence;
using Tenebit.Domain.Identity;
using Tenebit.Domain.JobProfiles;
using Tenebit.Domain.Licenses;
using Tenebit.Domain.Offboarding;
using Tenebit.Domain.People;
using Tenebit.Domain.Procedures;
using Tenebit.Domain.Reservations;
using Tenebit.Infrastructure.Data;
using Tenebit.Tests.Fakes;

namespace Tenebit.Tests;

/// <summary>
/// AUD3-013 regression guard. These are the tenant-owned relations called out by the audit plus
/// the legacy reservation catalog relations still present in the database. A future refactor that
/// accidentally returns to an Id-only FK must fail CI before it can reach a customer database.
/// </summary>
public sealed class TenantForeignKeyModelTests
{
    [Fact]
    public void Tenant_owned_foreign_keys_include_organization_id()
    {
        using var db = CreateContext();
        var model = db.Model;

        AssertTenantFk<Asset, AssetCategory>(model, nameof(Asset.OrganizationId), nameof(Asset.CategoryId));
        AssertTenantFk<AssetInspection, Asset>(model, nameof(AssetInspection.OrganizationId), nameof(AssetInspection.AssetId));
        AssertTenantFk<AssetInspection, Assignment>(model, nameof(AssetInspection.OrganizationId), nameof(AssetInspection.AssignmentId));
        AssertTenantFk<AssetInspection, OffboardingItem>(model, nameof(AssetInspection.OrganizationId), nameof(AssetInspection.OffboardingItemId));
        AssertTenantFk<ServiceTicket, Asset>(model, nameof(ServiceTicket.OrganizationId), nameof(ServiceTicket.AssetId));
        AssertTenantFk<Location, Location>(model, nameof(Location.OrganizationId), nameof(Location.ParentId));

        AssertTenantFk<ProcedureDocument, Procedure>(model, nameof(ProcedureDocument.OrganizationId), nameof(ProcedureDocument.ProcedureId));
        AssertTenantFk<JobProfileAssetCategory, JobProfile>(model, nameof(JobProfileAssetCategory.OrganizationId), nameof(JobProfileAssetCategory.JobProfileId));
        AssertTenantFk<JobProfileAssetCategory, AssetCategory>(model, nameof(JobProfileAssetCategory.OrganizationId), nameof(JobProfileAssetCategory.AssetCategoryId));
        AssertTenantFk<JobProfileProcedure, JobProfile>(model, nameof(JobProfileProcedure.OrganizationId), nameof(JobProfileProcedure.JobProfileId));
        AssertTenantFk<JobProfileProcedure, Procedure>(model, nameof(JobProfileProcedure.OrganizationId), nameof(JobProfileProcedure.ProcedureId));
        AssertTenantFk<LicenseSeat, License>(model, nameof(LicenseSeat.OrganizationId), nameof(LicenseSeat.LicenseId));
        AssertTenantFk<LicenseSeat, Person>(model, nameof(LicenseSeat.OrganizationId), nameof(LicenseSeat.PersonId));

        AssertTenantFk<Assignment, Person>(model, nameof(Assignment.OrganizationId), nameof(Assignment.PersonId));
        AssertTenantFk<AssignmentAsset, Assignment>(model, nameof(AssignmentAsset.OrganizationId), nameof(AssignmentAsset.AssignmentId));
        AssertTenantFk<AssignmentAsset, Asset>(model, nameof(AssignmentAsset.OrganizationId), nameof(AssignmentAsset.AssetId));
        AssertTenantFk<ProcedureAcceptance, Assignment>(model, nameof(ProcedureAcceptance.OrganizationId), nameof(ProcedureAcceptance.AssignmentId));
        AssertTenantFk<ProcedureAcceptance, Procedure>(model, nameof(ProcedureAcceptance.OrganizationId), nameof(ProcedureAcceptance.ProcedureId));
        AssertTenantFk<ProcedureAcceptance, Person>(model, nameof(ProcedureAcceptance.OrganizationId), nameof(ProcedureAcceptance.PersonId));

        AssertTenantFk<AssetAuditParticipant, Person>(model, nameof(AssetAuditParticipant.OrganizationId), nameof(AssetAuditParticipant.PersonId));
        AssertTenantFk<AssetAuditItem, Asset>(model, nameof(AssetAuditItem.OrganizationId), nameof(AssetAuditItem.AssetId));
        AssertTenantFk<AssetAuditItem, Person>(model, nameof(AssetAuditItem.OrganizationId), nameof(AssetAuditItem.ExpectedPersonId));

        AssertTenantFk<EquipmentReservation, Person>(model, nameof(EquipmentReservation.OrganizationId), nameof(EquipmentReservation.RequesterPersonId));
        AssertTenantFk<EquipmentReservation, Assignment>(model, nameof(EquipmentReservation.OrganizationId), nameof(EquipmentReservation.AssignmentId));
        AssertTenantFk<EquipmentReservationItem, AssetCategory>(model, nameof(EquipmentReservationItem.OrganizationId), nameof(EquipmentReservationItem.RequestedCategoryId));
        AssertTenantFk<EquipmentReservationItem, Asset>(model, nameof(EquipmentReservationItem.OrganizationId), nameof(EquipmentReservationItem.AssetId));
        AssertTenantFk<EquipmentReservationItem, Asset>(model, nameof(EquipmentReservationItem.OrganizationId), nameof(EquipmentReservationItem.OriginalAssetId));

        AssertTenantFk<OffboardingCase, Person>(model, nameof(OffboardingCase.OrganizationId), nameof(OffboardingCase.PersonId));
        AssertTenantFk<OffboardingItem, Asset>(model, nameof(OffboardingItem.OrganizationId), nameof(OffboardingItem.AssetId));
        AssertTenantFk<OffboardingItem, Assignment>(model, nameof(OffboardingItem.OrganizationId), nameof(OffboardingItem.AssignmentId));
        AssertTenantFk<OffboardingItem, License>(model, nameof(OffboardingItem.OrganizationId), nameof(OffboardingItem.LicenseId));
        AssertTenantFk<AssetEvidence, OffboardingItem>(model, nameof(AssetEvidence.OrganizationId), nameof(AssetEvidence.OffboardingItemId));
        AssertTenantFk<AssetEvidence, AssetAuditItem>(model, nameof(AssetEvidence.OrganizationId), nameof(AssetEvidence.AssetAuditItemId));
        AssertTenantFk<DashboardLayout, OrganizationUser>(model, nameof(DashboardLayout.OrganizationId), nameof(DashboardLayout.OrganizationUserId));
    }

    private static TenebitDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenebitDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;
        return new TenebitDbContext(options, new FakeFieldEncryptor());
    }

    private static void AssertTenantFk<TDependent, TPrincipal>(IModel model, params string[] propertyNames)
    {
        var dependent = model.GetEntityTypes().SingleOrDefault(x => x.ClrType == typeof(TDependent));
        Assert.NotNull(dependent);

        var match = dependent!.GetForeignKeys().SingleOrDefault(fk =>
            fk.PrincipalEntityType.ClrType == typeof(TPrincipal) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

        Assert.True(match is not null,
            $"{typeof(TDependent).Name} -> {typeof(TPrincipal).Name} must use composite FK ({string.Join(", ", propertyNames)}).");
        Assert.Equal(nameof(Asset.OrganizationId), match!.Properties[0].Name);
    }
}

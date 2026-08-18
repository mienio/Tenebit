using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260817214500_CompleteTenantCompositeForeignKeys")]
public partial class CompleteTenantCompositeForeignKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Owned/join rows previously carried only globally-unique parent IDs. Persist OrganizationId as well
        // so every user-controlled target reference can be protected by a composite tenant FK.
        migrationBuilder.Sql(
            """
            ALTER TABLE tenebit.assignment_assets ADD COLUMN "OrganizationId" uuid;
            UPDATE tenebit.assignment_assets c
            SET "OrganizationId" = p."OrganizationId"
            FROM tenebit.assignments p
            WHERE p."Id" = c."AssignmentId";
            ALTER TABLE tenebit.assignment_assets ALTER COLUMN "OrganizationId" SET NOT NULL;

            ALTER TABLE tenebit.job_profile_asset_categories ADD COLUMN "OrganizationId" uuid;
            UPDATE tenebit.job_profile_asset_categories c
            SET "OrganizationId" = p."OrganizationId"
            FROM tenebit.job_profiles p
            WHERE p."Id" = c."JobProfileId";
            ALTER TABLE tenebit.job_profile_asset_categories ALTER COLUMN "OrganizationId" SET NOT NULL;

            ALTER TABLE tenebit.job_profile_procedures ADD COLUMN "OrganizationId" uuid;
            UPDATE tenebit.job_profile_procedures c
            SET "OrganizationId" = p."OrganizationId"
            FROM tenebit.job_profiles p
            WHERE p."Id" = c."JobProfileId";
            ALTER TABLE tenebit.job_profile_procedures ALTER COLUMN "OrganizationId" SET NOT NULL;

            ALTER TABLE tenebit.license_seats ADD COLUMN "OrganizationId" uuid;
            UPDATE tenebit.license_seats c
            SET "OrganizationId" = p."OrganizationId"
            FROM tenebit.licenses p
            WHERE p."Id" = c."LicenseId";
            ALTER TABLE tenebit.license_seats ALTER COLUMN "OrganizationId" SET NOT NULL;
            """);

        // Fail closed before adding constraints. A migration must never silently relink contaminated rows.
        migrationBuilder.Sql(
            """
            DO $tenant_preflight$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM tenebit.assets c
                    LEFT JOIN tenebit.asset_categories p ON p."Id" = c."CategoryId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: assets.CategoryId contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.asset_inspections c
                    LEFT JOIN tenebit.assets p ON p."Id" = c."AssetId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: asset_inspections.AssetId contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.asset_inspections c
                    LEFT JOIN tenebit.assignments p ON p."Id" = c."AssignmentId"
                    WHERE c."AssignmentId" IS NOT NULL AND (p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId"))
                THEN RAISE EXCEPTION 'AUD3-013 preflight: asset_inspections.AssignmentId contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.asset_inspections c
                    LEFT JOIN tenebit.offboarding_items p ON p."Id" = c."OffboardingItemId"
                    WHERE c."OffboardingItemId" IS NOT NULL AND (p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId"))
                THEN RAISE EXCEPTION 'AUD3-013 preflight: asset_inspections.OffboardingItemId contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.service_tickets c
                    LEFT JOIN tenebit.assets p ON p."Id" = c."AssetId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: service_tickets.AssetId contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.asset_locations c
                    LEFT JOIN tenebit.asset_locations p ON p."Id" = c."ParentId"
                    WHERE c."ParentId" IS NOT NULL AND (p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId"))
                THEN RAISE EXCEPTION 'AUD3-013 preflight: asset_locations.ParentId contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.procedure_documents c
                    LEFT JOIN tenebit.procedures p ON p."Id" = c."ProcedureId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: procedure_documents.ProcedureId contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.job_profile_asset_categories c
                    LEFT JOIN tenebit.job_profiles owner ON owner."Id" = c."JobProfileId"
                    LEFT JOIN tenebit.asset_categories target ON target."Id" = c."AssetCategoryId"
                    WHERE owner."Id" IS NULL OR owner."OrganizationId" <> c."OrganizationId"
                       OR target."Id" IS NULL OR target."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: job_profile_asset_categories contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.job_profile_procedures c
                    LEFT JOIN tenebit.job_profiles owner ON owner."Id" = c."JobProfileId"
                    LEFT JOIN tenebit.procedures target ON target."Id" = c."ProcedureId"
                    WHERE owner."Id" IS NULL OR owner."OrganizationId" <> c."OrganizationId"
                       OR target."Id" IS NULL OR target."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: job_profile_procedures contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.license_seats c
                    LEFT JOIN tenebit.licenses owner ON owner."Id" = c."LicenseId"
                    LEFT JOIN tenebit.people target ON target."Id" = c."PersonId"
                    WHERE owner."Id" IS NULL OR owner."OrganizationId" <> c."OrganizationId"
                       OR target."Id" IS NULL OR target."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: license_seats contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.assignments c
                    LEFT JOIN tenebit.people p ON p."Id" = c."PersonId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: assignments.PersonId contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.assignment_assets c
                    LEFT JOIN tenebit.assignments owner ON owner."Id" = c."AssignmentId"
                    LEFT JOIN tenebit.assets target ON target."Id" = c."AssetId"
                    WHERE owner."Id" IS NULL OR owner."OrganizationId" <> c."OrganizationId"
                       OR target."Id" IS NULL OR target."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: assignment_assets contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.procedure_acceptances c
                    LEFT JOIN tenebit.assignments owner ON owner."Id" = c."AssignmentId"
                    LEFT JOIN tenebit.people person ON person."Id" = c."PersonId"
                    LEFT JOIN tenebit.procedures procedure ON procedure."Id" = c."ProcedureId"
                    WHERE owner."Id" IS NULL OR owner."OrganizationId" <> c."OrganizationId"
                       OR person."Id" IS NULL OR person."OrganizationId" <> c."OrganizationId"
                       OR procedure."Id" IS NULL OR procedure."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: procedure_acceptances contains an orphan/cross-tenant reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.asset_audit_participants c
                    LEFT JOIN tenebit.people p ON p."Id" = c."PersonId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: asset_audit_participants.PersonId contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.asset_audit_items c
                    LEFT JOIN tenebit.assets asset ON asset."Id" = c."AssetId"
                    LEFT JOIN tenebit.people person ON person."Id" = c."ExpectedPersonId"
                    WHERE asset."Id" IS NULL OR asset."OrganizationId" <> c."OrganizationId"
                       OR person."Id" IS NULL OR person."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: asset_audit_items contains an orphan/cross-tenant asset/person reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.equipment_reservations c
                    LEFT JOIN tenebit.people p ON p."Id" = c."RequesterPersonId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: equipment_reservations.RequesterPersonId contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.equipment_reservations c
                    LEFT JOIN tenebit.assignments p ON p."Id" = c."AssignmentId"
                    WHERE c."AssignmentId" IS NOT NULL AND (p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId"))
                THEN RAISE EXCEPTION 'AUD3-013 preflight: equipment_reservations.AssignmentId contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.equipment_reservation_items c
                    LEFT JOIN tenebit.asset_categories category ON category."Id" = c."RequestedCategoryId"
                    LEFT JOIN tenebit.assets asset ON asset."Id" = c."AssetId"
                    LEFT JOIN tenebit.assets original_asset ON original_asset."Id" = c."OriginalAssetId"
                    LEFT JOIN tenebit.equipment_kit_definitions kit ON kit."Id" = c."KitDefinitionId"
                    WHERE category."Id" IS NULL OR category."OrganizationId" <> c."OrganizationId"
                       OR (c."AssetId" IS NOT NULL AND (asset."Id" IS NULL OR asset."OrganizationId" <> c."OrganizationId"))
                       OR (c."OriginalAssetId" IS NOT NULL AND (original_asset."Id" IS NULL OR original_asset."OrganizationId" <> c."OrganizationId"))
                       OR (c."KitDefinitionId" IS NOT NULL AND (kit."Id" IS NULL OR kit."OrganizationId" <> c."OrganizationId")))
                THEN RAISE EXCEPTION 'AUD3-013 preflight: equipment_reservation_items contains an orphan/cross-tenant target'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.equipment_kit_definition_items c
                    LEFT JOIN tenebit.equipment_kit_definitions owner ON owner."Id" = c."KitDefinitionId"
                    LEFT JOIN tenebit.asset_categories category ON category."Id" = c."AssetCategoryId"
                    WHERE owner."Id" IS NULL OR owner."OrganizationId" <> c."OrganizationId"
                       OR category."Id" IS NULL OR category."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: equipment_kit_definition_items contains an orphan/cross-tenant target'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.offboarding_cases c
                    LEFT JOIN tenebit.people p ON p."Id" = c."PersonId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: offboarding_cases.PersonId contains an orphan/cross-tenant reference'; END IF;
                IF EXISTS (
                    SELECT 1 FROM tenebit.offboarding_items c
                    LEFT JOIN tenebit.assets asset ON asset."Id" = c."AssetId"
                    LEFT JOIN tenebit.assignments assignment ON assignment."Id" = c."AssignmentId"
                    LEFT JOIN tenebit.licenses license ON license."Id" = c."LicenseId"
                    WHERE (c."AssetId" IS NOT NULL AND (asset."Id" IS NULL OR asset."OrganizationId" <> c."OrganizationId"))
                       OR (c."AssignmentId" IS NOT NULL AND (assignment."Id" IS NULL OR assignment."OrganizationId" <> c."OrganizationId"))
                       OR (c."LicenseId" IS NOT NULL AND (license."Id" IS NULL OR license."OrganizationId" <> c."OrganizationId")))
                THEN RAISE EXCEPTION 'AUD3-013 preflight: offboarding_items contains an orphan/cross-tenant target'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.asset_evidence c
                    LEFT JOIN tenebit.offboarding_items offboarding_item ON offboarding_item."Id" = c."OffboardingItemId"
                    LEFT JOIN tenebit.asset_audit_items audit_item ON audit_item."Id" = c."AssetAuditItemId"
                    WHERE (c."OffboardingItemId" IS NOT NULL AND (offboarding_item."Id" IS NULL OR offboarding_item."OrganizationId" <> c."OrganizationId"))
                       OR (c."AssetAuditItemId" IS NOT NULL AND (audit_item."Id" IS NULL OR audit_item."OrganizationId" <> c."OrganizationId")))
                THEN RAISE EXCEPTION 'AUD3-013 preflight: asset_evidence contains an orphan/cross-tenant workflow reference'; END IF;

                IF EXISTS (
                    SELECT 1 FROM tenebit.dashboard_layouts c
                    LEFT JOIN tenebit.organization_users p ON p."Id" = c."OrganizationUserId"
                    WHERE p."Id" IS NULL OR p."OrganizationId" <> c."OrganizationId")
                THEN RAISE EXCEPTION 'AUD3-013 preflight: dashboard_layouts.OrganizationUserId contains an orphan/cross-tenant reference'; END IF;
            END
            $tenant_preflight$;
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE tenebit.asset_categories ADD CONSTRAINT "AK_asset_categories_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.procedures ADD CONSTRAINT "AK_procedures_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.job_profiles ADD CONSTRAINT "AK_job_profiles_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.licenses ADD CONSTRAINT "AK_licenses_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.asset_locations ADD CONSTRAINT "AK_asset_locations_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.offboarding_items ADD CONSTRAINT "AK_offboarding_items_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.asset_audit_items ADD CONSTRAINT "AK_asset_audit_items_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.organization_users ADD CONSTRAINT "AK_organization_users_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
            ALTER TABLE tenebit.equipment_kit_definitions ADD CONSTRAINT "AK_equipment_kit_definitions_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");

            ALTER TABLE tenebit.procedure_documents DROP CONSTRAINT IF EXISTS "FK_procedure_documents_procedures_ProcedureId";
            ALTER TABLE tenebit.assignment_assets DROP CONSTRAINT IF EXISTS "FK_assignment_assets_assignments_AssignmentId";
            ALTER TABLE tenebit.procedure_acceptances DROP CONSTRAINT IF EXISTS "FK_procedure_acceptances_assignments_AssignmentId";
            ALTER TABLE tenebit.job_profile_asset_categories DROP CONSTRAINT IF EXISTS "FK_job_profile_asset_categories_job_profiles_JobProfileId";
            ALTER TABLE tenebit.job_profile_procedures DROP CONSTRAINT IF EXISTS "FK_job_profile_procedures_job_profiles_JobProfileId";
            ALTER TABLE tenebit.license_seats DROP CONSTRAINT IF EXISTS "FK_license_seats_licenses_LicenseId";
            ALTER TABLE tenebit.equipment_kit_definition_items DROP CONSTRAINT IF EXISTS "FK_equipment_kit_definition_items_equipment_kit_definitions_Ki~";

            ALTER TABLE tenebit.assets ADD CONSTRAINT "FK_tenant_assets_category"
                FOREIGN KEY ("OrganizationId", "CategoryId") REFERENCES tenebit.asset_categories ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_inspections ADD CONSTRAINT "FK_tenant_inspections_asset"
                FOREIGN KEY ("OrganizationId", "AssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_inspections ADD CONSTRAINT "FK_tenant_inspections_assignment"
                FOREIGN KEY ("OrganizationId", "AssignmentId") REFERENCES tenebit.assignments ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_inspections ADD CONSTRAINT "FK_tenant_inspections_offboarding_item"
                FOREIGN KEY ("OrganizationId", "OffboardingItemId") REFERENCES tenebit.offboarding_items ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.service_tickets ADD CONSTRAINT "FK_tenant_service_tickets_asset"
                FOREIGN KEY ("OrganizationId", "AssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_locations ADD CONSTRAINT "FK_tenant_locations_parent"
                FOREIGN KEY ("OrganizationId", "ParentId") REFERENCES tenebit.asset_locations ("OrganizationId", "Id") ON DELETE RESTRICT;

            ALTER TABLE tenebit.procedure_documents ADD CONSTRAINT "FK_tenant_procedure_documents_procedure"
                FOREIGN KEY ("OrganizationId", "ProcedureId") REFERENCES tenebit.procedures ("OrganizationId", "Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.job_profile_asset_categories ADD CONSTRAINT "FK_tenant_jobprofile_categories_owner"
                FOREIGN KEY ("OrganizationId", "JobProfileId") REFERENCES tenebit.job_profiles ("OrganizationId", "Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.job_profile_asset_categories ADD CONSTRAINT "FK_tenant_jobprofile_categories_category"
                FOREIGN KEY ("OrganizationId", "AssetCategoryId") REFERENCES tenebit.asset_categories ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.job_profile_procedures ADD CONSTRAINT "FK_tenant_jobprofile_procedures_owner"
                FOREIGN KEY ("OrganizationId", "JobProfileId") REFERENCES tenebit.job_profiles ("OrganizationId", "Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.job_profile_procedures ADD CONSTRAINT "FK_tenant_jobprofile_procedures_procedure"
                FOREIGN KEY ("OrganizationId", "ProcedureId") REFERENCES tenebit.procedures ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.license_seats ADD CONSTRAINT "FK_tenant_license_seats_owner"
                FOREIGN KEY ("OrganizationId", "LicenseId") REFERENCES tenebit.licenses ("OrganizationId", "Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.license_seats ADD CONSTRAINT "FK_tenant_license_seats_person"
                FOREIGN KEY ("OrganizationId", "PersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;

            ALTER TABLE tenebit.assignments ADD CONSTRAINT "FK_tenant_assignments_person"
                FOREIGN KEY ("OrganizationId", "PersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.assignment_assets ADD CONSTRAINT "FK_tenant_assignment_assets_owner"
                FOREIGN KEY ("OrganizationId", "AssignmentId") REFERENCES tenebit.assignments ("OrganizationId", "Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.assignment_assets ADD CONSTRAINT "FK_tenant_assignment_assets_asset"
                FOREIGN KEY ("OrganizationId", "AssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.procedure_acceptances ADD CONSTRAINT "FK_tenant_procedure_acceptances_owner"
                FOREIGN KEY ("OrganizationId", "AssignmentId") REFERENCES tenebit.assignments ("OrganizationId", "Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.procedure_acceptances ADD CONSTRAINT "FK_tenant_procedure_acceptances_procedure"
                FOREIGN KEY ("OrganizationId", "ProcedureId") REFERENCES tenebit.procedures ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.procedure_acceptances ADD CONSTRAINT "FK_tenant_procedure_acceptances_person"
                FOREIGN KEY ("OrganizationId", "PersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;

            ALTER TABLE tenebit.asset_audit_participants ADD CONSTRAINT "FK_tenant_audit_participants_person"
                FOREIGN KEY ("OrganizationId", "PersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_audit_items ADD CONSTRAINT "FK_tenant_audit_items_asset"
                FOREIGN KEY ("OrganizationId", "AssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_audit_items ADD CONSTRAINT "FK_tenant_audit_items_expected_person"
                FOREIGN KEY ("OrganizationId", "ExpectedPersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;

            ALTER TABLE tenebit.equipment_reservations ADD CONSTRAINT "FK_tenant_reservations_requester"
                FOREIGN KEY ("OrganizationId", "RequesterPersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.equipment_reservations ADD CONSTRAINT "FK_tenant_reservations_assignment"
                FOREIGN KEY ("OrganizationId", "AssignmentId") REFERENCES tenebit.assignments ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.equipment_reservation_items ADD CONSTRAINT "FK_tenant_reservation_items_category"
                FOREIGN KEY ("OrganizationId", "RequestedCategoryId") REFERENCES tenebit.asset_categories ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.equipment_reservation_items ADD CONSTRAINT "FK_tenant_reservation_items_asset"
                FOREIGN KEY ("OrganizationId", "AssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.equipment_reservation_items ADD CONSTRAINT "FK_tenant_reservation_items_original_asset"
                FOREIGN KEY ("OrganizationId", "OriginalAssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.equipment_reservation_items ADD CONSTRAINT "FK_tenant_reservation_items_kit"
                FOREIGN KEY ("OrganizationId", "KitDefinitionId") REFERENCES tenebit.equipment_kit_definitions ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.equipment_kit_definition_items ADD CONSTRAINT "FK_tenant_kit_items_owner"
                FOREIGN KEY ("OrganizationId", "KitDefinitionId") REFERENCES tenebit.equipment_kit_definitions ("OrganizationId", "Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.equipment_kit_definition_items ADD CONSTRAINT "FK_tenant_kit_items_category"
                FOREIGN KEY ("OrganizationId", "AssetCategoryId") REFERENCES tenebit.asset_categories ("OrganizationId", "Id") ON DELETE RESTRICT;

            ALTER TABLE tenebit.offboarding_cases ADD CONSTRAINT "FK_tenant_offboarding_cases_person"
                FOREIGN KEY ("OrganizationId", "PersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.offboarding_items ADD CONSTRAINT "FK_tenant_offboarding_items_asset"
                FOREIGN KEY ("OrganizationId", "AssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.offboarding_items ADD CONSTRAINT "FK_tenant_offboarding_items_assignment"
                FOREIGN KEY ("OrganizationId", "AssignmentId") REFERENCES tenebit.assignments ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.offboarding_items ADD CONSTRAINT "FK_tenant_offboarding_items_license"
                FOREIGN KEY ("OrganizationId", "LicenseId") REFERENCES tenebit.licenses ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_evidence ADD CONSTRAINT "FK_tenant_evidence_offboarding_item"
                FOREIGN KEY ("OrganizationId", "OffboardingItemId") REFERENCES tenebit.offboarding_items ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.asset_evidence ADD CONSTRAINT "FK_tenant_evidence_audit_item"
                FOREIGN KEY ("OrganizationId", "AssetAuditItemId") REFERENCES tenebit.asset_audit_items ("OrganizationId", "Id") ON DELETE RESTRICT;
            ALTER TABLE tenebit.dashboard_layouts ADD CONSTRAINT "FK_tenant_dashboard_layout_user"
                FOREIGN KEY ("OrganizationId", "OrganizationUserId") REFERENCES tenebit.organization_users ("OrganizationId", "Id") ON DELETE CASCADE;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX "IX_tenant_assets_category" ON tenebit.assets ("OrganizationId", "CategoryId");
            CREATE INDEX "IX_tenant_inspections_assignment" ON tenebit.asset_inspections ("OrganizationId", "AssignmentId");
            CREATE INDEX "IX_tenant_inspections_offboarding_item" ON tenebit.asset_inspections ("OrganizationId", "OffboardingItemId");
            CREATE INDEX "IX_tenant_jobprofile_categories_owner" ON tenebit.job_profile_asset_categories ("OrganizationId", "JobProfileId");
            CREATE INDEX "IX_tenant_jobprofile_categories_category" ON tenebit.job_profile_asset_categories ("OrganizationId", "AssetCategoryId");
            CREATE INDEX "IX_tenant_jobprofile_procedures_owner" ON tenebit.job_profile_procedures ("OrganizationId", "JobProfileId");
            CREATE INDEX "IX_tenant_jobprofile_procedures_procedure" ON tenebit.job_profile_procedures ("OrganizationId", "ProcedureId");
            CREATE INDEX "IX_tenant_license_seats_owner" ON tenebit.license_seats ("OrganizationId", "LicenseId");
            CREATE INDEX "IX_tenant_license_seats_person" ON tenebit.license_seats ("OrganizationId", "PersonId");
            CREATE INDEX "IX_tenant_assignments_person" ON tenebit.assignments ("OrganizationId", "PersonId");
            CREATE INDEX "IX_tenant_assignment_assets_owner" ON tenebit.assignment_assets ("OrganizationId", "AssignmentId");
            CREATE INDEX "IX_tenant_assignment_assets_asset" ON tenebit.assignment_assets ("OrganizationId", "AssetId");
            CREATE INDEX "IX_tenant_procedure_acceptances_owner" ON tenebit.procedure_acceptances ("OrganizationId", "AssignmentId");
            CREATE INDEX "IX_tenant_procedure_acceptances_procedure" ON tenebit.procedure_acceptances ("OrganizationId", "ProcedureId");
            CREATE INDEX "IX_tenant_procedure_acceptances_person" ON tenebit.procedure_acceptances ("OrganizationId", "PersonId");
            CREATE INDEX "IX_tenant_audit_participants_person" ON tenebit.asset_audit_participants ("OrganizationId", "PersonId");
            CREATE INDEX "IX_tenant_audit_items_asset" ON tenebit.asset_audit_items ("OrganizationId", "AssetId");
            CREATE INDEX "IX_tenant_audit_items_expected_person" ON tenebit.asset_audit_items ("OrganizationId", "ExpectedPersonId");
            CREATE INDEX "IX_tenant_reservations_assignment" ON tenebit.equipment_reservations ("OrganizationId", "AssignmentId");
            CREATE INDEX "IX_tenant_reservation_items_category" ON tenebit.equipment_reservation_items ("OrganizationId", "RequestedCategoryId");
            CREATE INDEX "IX_tenant_reservation_items_original_asset" ON tenebit.equipment_reservation_items ("OrganizationId", "OriginalAssetId");
            CREATE INDEX "IX_tenant_reservation_items_kit" ON tenebit.equipment_reservation_items ("OrganizationId", "KitDefinitionId");
            CREATE INDEX "IX_tenant_kit_items_category" ON tenebit.equipment_kit_definition_items ("OrganizationId", "AssetCategoryId");
            CREATE INDEX "IX_tenant_offboarding_items_asset" ON tenebit.offboarding_items ("OrganizationId", "AssetId");
            CREATE INDEX "IX_tenant_offboarding_items_assignment" ON tenebit.offboarding_items ("OrganizationId", "AssignmentId");
            CREATE INDEX "IX_tenant_offboarding_items_license" ON tenebit.offboarding_items ("OrganizationId", "LicenseId");
            CREATE INDEX "IX_tenant_evidence_offboarding_item" ON tenebit.asset_evidence ("OrganizationId", "OffboardingItemId");
            CREATE INDEX "IX_tenant_evidence_audit_item" ON tenebit.asset_evidence ("OrganizationId", "AssetAuditItemId");
            CREATE INDEX "IX_tenant_dashboard_layout_user" ON tenebit.dashboard_layouts ("OrganizationId", "OrganizationUserId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE tenebit.dashboard_layouts DROP CONSTRAINT IF EXISTS "FK_tenant_dashboard_layout_user";
            ALTER TABLE tenebit.asset_evidence DROP CONSTRAINT IF EXISTS "FK_tenant_evidence_audit_item";
            ALTER TABLE tenebit.asset_evidence DROP CONSTRAINT IF EXISTS "FK_tenant_evidence_offboarding_item";
            ALTER TABLE tenebit.offboarding_items DROP CONSTRAINT IF EXISTS "FK_tenant_offboarding_items_license";
            ALTER TABLE tenebit.offboarding_items DROP CONSTRAINT IF EXISTS "FK_tenant_offboarding_items_assignment";
            ALTER TABLE tenebit.offboarding_items DROP CONSTRAINT IF EXISTS "FK_tenant_offboarding_items_asset";
            ALTER TABLE tenebit.offboarding_cases DROP CONSTRAINT IF EXISTS "FK_tenant_offboarding_cases_person";
            ALTER TABLE tenebit.equipment_kit_definition_items DROP CONSTRAINT IF EXISTS "FK_tenant_kit_items_category";
            ALTER TABLE tenebit.equipment_kit_definition_items DROP CONSTRAINT IF EXISTS "FK_tenant_kit_items_owner";
            ALTER TABLE tenebit.equipment_reservation_items DROP CONSTRAINT IF EXISTS "FK_tenant_reservation_items_kit";
            ALTER TABLE tenebit.equipment_reservation_items DROP CONSTRAINT IF EXISTS "FK_tenant_reservation_items_original_asset";
            ALTER TABLE tenebit.equipment_reservation_items DROP CONSTRAINT IF EXISTS "FK_tenant_reservation_items_asset";
            ALTER TABLE tenebit.equipment_reservation_items DROP CONSTRAINT IF EXISTS "FK_tenant_reservation_items_category";
            ALTER TABLE tenebit.equipment_reservations DROP CONSTRAINT IF EXISTS "FK_tenant_reservations_assignment";
            ALTER TABLE tenebit.equipment_reservations DROP CONSTRAINT IF EXISTS "FK_tenant_reservations_requester";
            ALTER TABLE tenebit.asset_audit_items DROP CONSTRAINT IF EXISTS "FK_tenant_audit_items_expected_person";
            ALTER TABLE tenebit.asset_audit_items DROP CONSTRAINT IF EXISTS "FK_tenant_audit_items_asset";
            ALTER TABLE tenebit.asset_audit_participants DROP CONSTRAINT IF EXISTS "FK_tenant_audit_participants_person";
            ALTER TABLE tenebit.procedure_acceptances DROP CONSTRAINT IF EXISTS "FK_tenant_procedure_acceptances_person";
            ALTER TABLE tenebit.procedure_acceptances DROP CONSTRAINT IF EXISTS "FK_tenant_procedure_acceptances_procedure";
            ALTER TABLE tenebit.procedure_acceptances DROP CONSTRAINT IF EXISTS "FK_tenant_procedure_acceptances_owner";
            ALTER TABLE tenebit.assignment_assets DROP CONSTRAINT IF EXISTS "FK_tenant_assignment_assets_asset";
            ALTER TABLE tenebit.assignment_assets DROP CONSTRAINT IF EXISTS "FK_tenant_assignment_assets_owner";
            ALTER TABLE tenebit.assignments DROP CONSTRAINT IF EXISTS "FK_tenant_assignments_person";
            ALTER TABLE tenebit.license_seats DROP CONSTRAINT IF EXISTS "FK_tenant_license_seats_person";
            ALTER TABLE tenebit.license_seats DROP CONSTRAINT IF EXISTS "FK_tenant_license_seats_owner";
            ALTER TABLE tenebit.job_profile_procedures DROP CONSTRAINT IF EXISTS "FK_tenant_jobprofile_procedures_procedure";
            ALTER TABLE tenebit.job_profile_procedures DROP CONSTRAINT IF EXISTS "FK_tenant_jobprofile_procedures_owner";
            ALTER TABLE tenebit.job_profile_asset_categories DROP CONSTRAINT IF EXISTS "FK_tenant_jobprofile_categories_category";
            ALTER TABLE tenebit.job_profile_asset_categories DROP CONSTRAINT IF EXISTS "FK_tenant_jobprofile_categories_owner";
            ALTER TABLE tenebit.procedure_documents DROP CONSTRAINT IF EXISTS "FK_tenant_procedure_documents_procedure";
            ALTER TABLE tenebit.asset_locations DROP CONSTRAINT IF EXISTS "FK_tenant_locations_parent";
            ALTER TABLE tenebit.service_tickets DROP CONSTRAINT IF EXISTS "FK_tenant_service_tickets_asset";
            ALTER TABLE tenebit.asset_inspections DROP CONSTRAINT IF EXISTS "FK_tenant_inspections_offboarding_item";
            ALTER TABLE tenebit.asset_inspections DROP CONSTRAINT IF EXISTS "FK_tenant_inspections_assignment";
            ALTER TABLE tenebit.asset_inspections DROP CONSTRAINT IF EXISTS "FK_tenant_inspections_asset";
            ALTER TABLE tenebit.assets DROP CONSTRAINT IF EXISTS "FK_tenant_assets_category";

            DROP INDEX IF EXISTS tenebit."IX_tenant_assets_category";
            DROP INDEX IF EXISTS tenebit."IX_tenant_inspections_assignment";
            DROP INDEX IF EXISTS tenebit."IX_tenant_inspections_offboarding_item";
            DROP INDEX IF EXISTS tenebit."IX_tenant_jobprofile_categories_owner";
            DROP INDEX IF EXISTS tenebit."IX_tenant_jobprofile_categories_category";
            DROP INDEX IF EXISTS tenebit."IX_tenant_jobprofile_procedures_owner";
            DROP INDEX IF EXISTS tenebit."IX_tenant_jobprofile_procedures_procedure";
            DROP INDEX IF EXISTS tenebit."IX_tenant_license_seats_owner";
            DROP INDEX IF EXISTS tenebit."IX_tenant_license_seats_person";
            DROP INDEX IF EXISTS tenebit."IX_tenant_assignments_person";
            DROP INDEX IF EXISTS tenebit."IX_tenant_assignment_assets_owner";
            DROP INDEX IF EXISTS tenebit."IX_tenant_assignment_assets_asset";
            DROP INDEX IF EXISTS tenebit."IX_tenant_procedure_acceptances_owner";
            DROP INDEX IF EXISTS tenebit."IX_tenant_procedure_acceptances_procedure";
            DROP INDEX IF EXISTS tenebit."IX_tenant_procedure_acceptances_person";
            DROP INDEX IF EXISTS tenebit."IX_tenant_audit_participants_person";
            DROP INDEX IF EXISTS tenebit."IX_tenant_audit_items_asset";
            DROP INDEX IF EXISTS tenebit."IX_tenant_audit_items_expected_person";
            DROP INDEX IF EXISTS tenebit."IX_tenant_reservations_assignment";
            DROP INDEX IF EXISTS tenebit."IX_tenant_reservation_items_category";
            DROP INDEX IF EXISTS tenebit."IX_tenant_reservation_items_original_asset";
            DROP INDEX IF EXISTS tenebit."IX_tenant_reservation_items_kit";
            DROP INDEX IF EXISTS tenebit."IX_tenant_kit_items_category";
            DROP INDEX IF EXISTS tenebit."IX_tenant_offboarding_items_asset";
            DROP INDEX IF EXISTS tenebit."IX_tenant_offboarding_items_assignment";
            DROP INDEX IF EXISTS tenebit."IX_tenant_offboarding_items_license";
            DROP INDEX IF EXISTS tenebit."IX_tenant_evidence_offboarding_item";
            DROP INDEX IF EXISTS tenebit."IX_tenant_evidence_audit_item";
            DROP INDEX IF EXISTS tenebit."IX_tenant_dashboard_layout_user";

            ALTER TABLE tenebit.equipment_kit_definition_items ADD CONSTRAINT "FK_equipment_kit_definition_items_equipment_kit_definitions_Ki~"
                FOREIGN KEY ("KitDefinitionId") REFERENCES tenebit.equipment_kit_definitions ("Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.license_seats ADD CONSTRAINT "FK_license_seats_licenses_LicenseId"
                FOREIGN KEY ("LicenseId") REFERENCES tenebit.licenses ("Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.job_profile_procedures ADD CONSTRAINT "FK_job_profile_procedures_job_profiles_JobProfileId"
                FOREIGN KEY ("JobProfileId") REFERENCES tenebit.job_profiles ("Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.job_profile_asset_categories ADD CONSTRAINT "FK_job_profile_asset_categories_job_profiles_JobProfileId"
                FOREIGN KEY ("JobProfileId") REFERENCES tenebit.job_profiles ("Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.procedure_acceptances ADD CONSTRAINT "FK_procedure_acceptances_assignments_AssignmentId"
                FOREIGN KEY ("AssignmentId") REFERENCES tenebit.assignments ("Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.assignment_assets ADD CONSTRAINT "FK_assignment_assets_assignments_AssignmentId"
                FOREIGN KEY ("AssignmentId") REFERENCES tenebit.assignments ("Id") ON DELETE CASCADE;
            ALTER TABLE tenebit.procedure_documents ADD CONSTRAINT "FK_procedure_documents_procedures_ProcedureId"
                FOREIGN KEY ("ProcedureId") REFERENCES tenebit.procedures ("Id") ON DELETE CASCADE;

            ALTER TABLE tenebit.equipment_kit_definitions DROP CONSTRAINT IF EXISTS "AK_equipment_kit_definitions_OrganizationId_Id";
            ALTER TABLE tenebit.organization_users DROP CONSTRAINT IF EXISTS "AK_organization_users_OrganizationId_Id";
            ALTER TABLE tenebit.asset_audit_items DROP CONSTRAINT IF EXISTS "AK_asset_audit_items_OrganizationId_Id";
            ALTER TABLE tenebit.offboarding_items DROP CONSTRAINT IF EXISTS "AK_offboarding_items_OrganizationId_Id";
            ALTER TABLE tenebit.asset_locations DROP CONSTRAINT IF EXISTS "AK_asset_locations_OrganizationId_Id";
            ALTER TABLE tenebit.licenses DROP CONSTRAINT IF EXISTS "AK_licenses_OrganizationId_Id";
            ALTER TABLE tenebit.job_profiles DROP CONSTRAINT IF EXISTS "AK_job_profiles_OrganizationId_Id";
            ALTER TABLE tenebit.procedures DROP CONSTRAINT IF EXISTS "AK_procedures_OrganizationId_Id";
            ALTER TABLE tenebit.asset_categories DROP CONSTRAINT IF EXISTS "AK_asset_categories_OrganizationId_Id";

            ALTER TABLE tenebit.license_seats DROP COLUMN "OrganizationId";
            ALTER TABLE tenebit.job_profile_procedures DROP COLUMN "OrganizationId";
            ALTER TABLE tenebit.job_profile_asset_categories DROP COLUMN "OrganizationId";
            ALTER TABLE tenebit.assignment_assets DROP COLUMN "OrganizationId";
            """);
    }
}

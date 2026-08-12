using Microsoft.EntityFrameworkCore;

namespace Tenebit.Infrastructure.Data;

public static class TenebitSchemaPatch
{
    public static async Task ApplyAsync(TenebitDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(Sql, cancellationToken);
    }

    private const string Sql = """
CREATE SCHEMA IF NOT EXISTS tenebit;

CREATE TABLE IF NOT EXISTS tenebit.procedure_documents (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "ProcedureId" uuid NOT NULL,
    "FileName" character varying(260) NOT NULL,
    "ContentType" character varying(160) NOT NULL,
    "SizeBytes" bigint NOT NULL,
    "Content" bytea NOT NULL,
    "UploadedBy" character varying(240) NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_procedure_documents_OrganizationId_ProcedureId_UploadedAt"
    ON tenebit.procedure_documents ("OrganizationId", "ProcedureId", "UploadedAt");

CREATE TABLE IF NOT EXISTS tenebit.organization_users (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "Email" character varying(240) NOT NULL,
    "DisplayName" character varying(160) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_organization_users_OrganizationId_Email"
    ON tenebit.organization_users ("OrganizationId", "Email");

CREATE TABLE IF NOT EXISTS tenebit.organization_user_roles (
    "UserId" uuid NOT NULL,
    "Role" character varying(80) NOT NULL,
    CONSTRAINT "PK_organization_user_roles" PRIMARY KEY ("UserId", "Role")
);

CREATE TABLE IF NOT EXISTS tenebit.job_profiles (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "Name" character varying(140) NOT NULL,
    "Description" character varying(800) NULL,
    "DefaultManagerId" uuid NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_job_profiles_OrganizationId_Name"
    ON tenebit.job_profiles ("OrganizationId", "Name");

CREATE TABLE IF NOT EXISTS tenebit.job_profile_asset_categories (
    "JobProfileId" uuid NOT NULL,
    "AssetCategoryId" uuid NOT NULL,
    CONSTRAINT "PK_job_profile_asset_categories" PRIMARY KEY ("JobProfileId", "AssetCategoryId")
);

CREATE TABLE IF NOT EXISTS tenebit.job_profile_procedures (
    "JobProfileId" uuid NOT NULL,
    "ProcedureId" uuid NOT NULL,
    CONSTRAINT "PK_job_profile_procedures" PRIMARY KEY ("JobProfileId", "ProcedureId")
);

CREATE TABLE IF NOT EXISTS tenebit.asset_status_settings (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "StatusKey" character varying(60) NOT NULL,
    "Label" character varying(80) NOT NULL,
    "SortOrder" integer NOT NULL,
    "IsEnabled" boolean NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_asset_status_settings_OrganizationId_StatusKey"
    ON tenebit.asset_status_settings ("OrganizationId", "StatusKey");

CREATE TABLE IF NOT EXISTS tenebit.person_relation_types (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "Name" character varying(80) NOT NULL,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_person_relation_types_OrganizationId_Name"
    ON tenebit.person_relation_types ("OrganizationId", "Name");

CREATE TABLE IF NOT EXISTS tenebit.licenses (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Vendor" character varying(160) NULL,
    "LicenseKey" character varying(400) NULL,
    "SeatsTotal" integer NOT NULL,
    "ExpiresAt" date NULL,
    "Notes" character varying(800) NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_licenses_OrganizationId" ON tenebit.licenses ("OrganizationId");

CREATE TABLE IF NOT EXISTS tenebit.license_seats (
    "LicenseId" uuid NOT NULL,
    "PersonId" uuid NOT NULL,
    "AssignedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_license_seats" PRIMARY KEY ("LicenseId", "PersonId")
);

CREATE TABLE IF NOT EXISTS tenebit.role_permissions (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "RoleKey" character varying(60) NOT NULL,
    "PermissionKey" character varying(80) NOT NULL,
    "Allowed" boolean NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_role_permissions_OrganizationId_RoleKey_PermissionKey"
    ON tenebit.role_permissions ("OrganizationId", "RoleKey", "PermissionKey");

INSERT INTO tenebit.person_relation_types ("Id", "OrganizationId", "Name", "SortOrder", "CreatedAt")
SELECT gen_random_uuid(), o."Id", CASE WHEN o."Language" = 'pl' THEN v."NamePl" ELSE v."NameEn" END, v."SortOrder", now()
FROM tenebit.organizations o
CROSS JOIN (VALUES ('Pracownik', 'Employee', 10), ('Kontraktor', 'Contractor', 20), ('Dostawca', 'Vendor', 30)) AS v("NamePl", "NameEn", "SortOrder")
WHERE NOT EXISTS (SELECT 1 FROM tenebit.person_relation_types p WHERE p."OrganizationId" = o."Id");

CREATE TABLE IF NOT EXISTS tenebit.subscriptions (
    "Id" uuid PRIMARY KEY,
    "OrganizationId" uuid NOT NULL,
    "PlanKey" character varying(40) NOT NULL,
    "Status" character varying(40) NOT NULL,
    "CurrentPeriodStart" timestamp with time zone NOT NULL,
    "CurrentPeriodEnd" timestamp with time zone NOT NULL,
    "CancelledAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_subscriptions_OrganizationId"
    ON tenebit.subscriptions ("OrganizationId");

ALTER TABLE tenebit.asset_categories ADD COLUMN IF NOT EXISTS "Icon" character varying(40) NULL;
ALTER TABLE tenebit.asset_categories ADD COLUMN IF NOT EXISTS "SortOrder" integer NOT NULL DEFAULT 0;

ALTER TABLE tenebit.organization_users ADD COLUMN IF NOT EXISTS "PasswordHash" character varying(400) NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_organization_users_Email"
    ON tenebit.organization_users ("Email");

CREATE TABLE IF NOT EXISTS tenebit.external_logins (
    "Id" uuid PRIMARY KEY,
    "OrganizationUserId" uuid NOT NULL,
    "Provider" character varying(40) NOT NULL,
    "ProviderUserId" character varying(240) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_external_logins_Provider_ProviderUserId"
    ON tenebit.external_logins ("Provider", "ProviderUserId");

CREATE INDEX IF NOT EXISTS "IX_external_logins_OrganizationUserId"
    ON tenebit.external_logins ("OrganizationUserId");

CREATE TABLE IF NOT EXISTS tenebit.password_reset_tokens (
    "Id" uuid PRIMARY KEY,
    "OrganizationUserId" uuid NOT NULL,
    "TokenHash" character varying(120) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_password_reset_tokens_TokenHash"
    ON tenebit.password_reset_tokens ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_password_reset_tokens_OrganizationUserId"
    ON tenebit.password_reset_tokens ("OrganizationUserId");

ALTER TABLE tenebit.organization_users ADD COLUMN IF NOT EXISTS "IsEmailVerified" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE tenebit.organization_users ADD COLUMN IF NOT EXISTS "TotpSecret" character varying(64) NULL;
ALTER TABLE tenebit.organization_users ADD COLUMN IF NOT EXISTS "IsTwoFactorEnabled" boolean NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS tenebit.email_verification_tokens (
    "Id" uuid PRIMARY KEY,
    "OrganizationUserId" uuid NOT NULL,
    "TokenHash" character varying(120) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_email_verification_tokens_TokenHash"
    ON tenebit.email_verification_tokens ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_email_verification_tokens_OrganizationUserId"
    ON tenebit.email_verification_tokens ("OrganizationUserId");

CREATE TABLE IF NOT EXISTS tenebit.refresh_tokens (
    "Id" uuid PRIMARY KEY,
    "OrganizationUserId" uuid NOT NULL,
    "TokenHash" character varying(120) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_refresh_tokens_TokenHash"
    ON tenebit.refresh_tokens ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_refresh_tokens_OrganizationUserId"
    ON tenebit.refresh_tokens ("OrganizationUserId");

CREATE TABLE IF NOT EXISTS tenebit.device_trust_tokens (
    "Id" uuid PRIMARY KEY,
    "OrganizationUserId" uuid NOT NULL,
    "TokenHash" character varying(120) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_device_trust_tokens_UserId_TokenHash"
    ON tenebit.device_trust_tokens ("OrganizationUserId", "TokenHash");

CREATE TABLE IF NOT EXISTS tenebit.two_factor_recovery_codes (
    "Id" uuid PRIMARY KEY,
    "OrganizationUserId" uuid NOT NULL,
    "CodeHash" character varying(120) NOT NULL,
    "UsedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_two_factor_recovery_codes_OrganizationUserId"
    ON tenebit.two_factor_recovery_codes ("OrganizationUserId");

ALTER TABLE tenebit.asset_status_settings ADD COLUMN IF NOT EXISTS "Color" character varying(9) NOT NULL DEFAULT '#1d4ed8';
ALTER TABLE tenebit.asset_status_settings ADD COLUMN IF NOT EXISTS "BackgroundColor" character varying(9) NOT NULL DEFAULT '#eff6ff';
""";
}

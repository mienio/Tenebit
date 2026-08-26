CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'tenebit') THEN
            CREATE SCHEMA tenebit;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.activity_logs (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Action" character varying(120) NOT NULL,
        "EntityType" character varying(80) NOT NULL,
        "EntityId" uuid,
        "ActorSubject" character varying(240) NOT NULL,
        "Details" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_activity_logs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.asset_categories (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Type" character varying(40) NOT NULL,
        "Description" character varying(600),
        "Icon" character varying(40),
        "IsSystem" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_asset_categories" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.asset_status_settings (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "StatusKey" character varying(60) NOT NULL,
        "Label" character varying(80) NOT NULL,
        "SortOrder" integer NOT NULL,
        "IsEnabled" boolean NOT NULL,
        CONSTRAINT "PK_asset_status_settings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.assets (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "CategoryId" uuid NOT NULL,
        "Name" character varying(180) NOT NULL,
        "AssetTag" character varying(80) NOT NULL,
        "SerialNumber" character varying(120),
        "Status" character varying(40) NOT NULL,
        "AssignedPersonId" uuid,
        "TeamId" uuid,
        "Location" character varying(180),
        "Manufacturer" character varying(120),
        "Model" character varying(120),
        "PurchasePrice" numeric(18,2),
        "Currency" character varying(8),
        "PurchaseDate" date,
        "WarrantyUntil" date,
        "QrCodePayload" character varying(160) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_assets" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.assignments (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "Status" character varying(40) NOT NULL,
        "IssuedAt" timestamp with time zone NOT NULL,
        "DueDate" date,
        "AcceptedAt" timestamp with time zone,
        "ReturnedAt" timestamp with time zone,
        "Notes" character varying(800),
        "ProtocolNumber" character varying(80) NOT NULL,
        "CreatedBy" character varying(240) NOT NULL,
        CONSTRAINT "PK_assignments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.device_trust_tokens (
        "Id" uuid NOT NULL,
        "OrganizationUserId" uuid NOT NULL,
        "TokenHash" character varying(120) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_device_trust_tokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.email_verification_tokens (
        "Id" uuid NOT NULL,
        "OrganizationUserId" uuid NOT NULL,
        "TokenHash" character varying(120) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "UsedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_email_verification_tokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.external_logins (
        "Id" uuid NOT NULL,
        "OrganizationUserId" uuid NOT NULL,
        "Provider" character varying(40) NOT NULL,
        "ProviderUserId" character varying(240) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_external_logins" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.job_profiles (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(140) NOT NULL,
        "Description" character varying(800),
        "DefaultManagerId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_job_profiles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.organization_users (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Email" character varying(240) NOT NULL,
        "DisplayName" character varying(160) NOT NULL,
        "IsActive" boolean NOT NULL,
        "PasswordHash" character varying(400),
        "IsEmailVerified" boolean NOT NULL,
        "TotpSecret" character varying(64),
        "IsTwoFactorEnabled" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_organization_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.organizations (
        "Id" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Country" character varying(8) NOT NULL,
        "Language" character varying(8) NOT NULL,
        "Currency" character varying(8) NOT NULL,
        "TimeZone" character varying(80) NOT NULL,
        "LogoUrl" character varying(600),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_organizations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.password_reset_tokens (
        "Id" uuid NOT NULL,
        "OrganizationUserId" uuid NOT NULL,
        "TokenHash" character varying(120) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "UsedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_password_reset_tokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.people (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "FirstName" character varying(80) NOT NULL,
        "LastName" character varying(120) NOT NULL,
        "Email" character varying(240) NOT NULL,
        "Phone" character varying(40),
        "EmployeeNumber" character varying(80),
        "RelationType" character varying(40) NOT NULL,
        "JobTitle" character varying(120),
        "TeamId" uuid,
        "ManagerId" uuid,
        "Location" character varying(180),
        "CostCenter" character varying(80),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_people" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.procedures (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Title" character varying(180) NOT NULL,
        "Version" character varying(40) NOT NULL,
        "Owner" character varying(120) NOT NULL,
        "Status" character varying(40) NOT NULL,
        "AppliesTo" character varying(240),
        "ReviewDate" date,
        "RequiresAcceptance" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "PublishedAt" timestamp with time zone,
        CONSTRAINT "PK_procedures" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.refresh_tokens (
        "Id" uuid NOT NULL,
        "OrganizationUserId" uuid NOT NULL,
        "TokenHash" character varying(120) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "RevokedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.sent_alerts (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "AlertKey" character varying(60) NOT NULL,
        "EntityId" uuid NOT NULL,
        "SentAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_sent_alerts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.subscriptions (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "PlanKey" character varying(40) NOT NULL,
        "Status" character varying(40) NOT NULL,
        "CurrentPeriodStart" timestamp with time zone NOT NULL,
        "CurrentPeriodEnd" timestamp with time zone NOT NULL,
        "CancelledAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_subscriptions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.teams (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "ManagerId" uuid,
        "CostCenter" character varying(80),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_teams" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.asset_field_definitions (
        "CategoryId" uuid NOT NULL,
        "Id" uuid NOT NULL,
        "Key" character varying(80) NOT NULL,
        "Label" character varying(120) NOT NULL,
        "FieldType" character varying(40) NOT NULL,
        "Options" character varying(1000),
        "Required" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        CONSTRAINT "PK_asset_field_definitions" PRIMARY KEY ("CategoryId", "Id"),
        CONSTRAINT "FK_asset_field_definitions_asset_categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES tenebit.asset_categories ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.asset_field_values (
        "AssetId" uuid NOT NULL,
        "FieldKey" character varying(80) NOT NULL,
        "Value" character varying(2000) NOT NULL,
        CONSTRAINT "PK_asset_field_values" PRIMARY KEY ("AssetId", "FieldKey"),
        CONSTRAINT "FK_asset_field_values_assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES tenebit.assets ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.assignment_assets (
        "AssignmentId" uuid NOT NULL,
        "AssetId" uuid NOT NULL,
        "IssueCondition" character varying(400) NOT NULL,
        "ReturnCondition" character varying(400),
        CONSTRAINT "PK_assignment_assets" PRIMARY KEY ("AssignmentId", "AssetId"),
        CONSTRAINT "FK_assignment_assets_assignments_AssignmentId" FOREIGN KEY ("AssignmentId") REFERENCES tenebit.assignments ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.procedure_acceptances (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "ProcedureId" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "AssignmentId" uuid NOT NULL,
        "Status" character varying(40) NOT NULL,
        "SentAt" timestamp with time zone NOT NULL,
        "AcceptedAt" timestamp with time zone,
        CONSTRAINT "PK_procedure_acceptances" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_procedure_acceptances_assignments_AssignmentId" FOREIGN KEY ("AssignmentId") REFERENCES tenebit.assignments ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.job_profile_asset_categories (
        "JobProfileId" uuid NOT NULL,
        "AssetCategoryId" uuid NOT NULL,
        CONSTRAINT "PK_job_profile_asset_categories" PRIMARY KEY ("JobProfileId", "AssetCategoryId"),
        CONSTRAINT "FK_job_profile_asset_categories_job_profiles_JobProfileId" FOREIGN KEY ("JobProfileId") REFERENCES tenebit.job_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.job_profile_procedures (
        "JobProfileId" uuid NOT NULL,
        "ProcedureId" uuid NOT NULL,
        CONSTRAINT "PK_job_profile_procedures" PRIMARY KEY ("JobProfileId", "ProcedureId"),
        CONSTRAINT "FK_job_profile_procedures_job_profiles_JobProfileId" FOREIGN KEY ("JobProfileId") REFERENCES tenebit.job_profiles ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.organization_user_roles (
        "UserId" uuid NOT NULL,
        "Role" character varying(80) NOT NULL,
        CONSTRAINT "PK_organization_user_roles" PRIMARY KEY ("UserId", "Role"),
        CONSTRAINT "FK_organization_user_roles_organization_users_UserId" FOREIGN KEY ("UserId") REFERENCES tenebit.organization_users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.procedure_documents (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "ProcedureId" uuid NOT NULL,
        "FileName" character varying(260) NOT NULL,
        "ContentType" character varying(160) NOT NULL,
        "SizeBytes" bigint NOT NULL,
        "Content" bytea NOT NULL,
        "UploadedBy" character varying(240) NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_procedure_documents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_procedure_documents_procedures_ProcedureId" FOREIGN KEY ("ProcedureId") REFERENCES tenebit.procedures ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_activity_logs_OrganizationId_CreatedAt" ON tenebit.activity_logs ("OrganizationId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_asset_categories_OrganizationId_Name" ON tenebit.asset_categories ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_asset_status_settings_OrganizationId_StatusKey" ON tenebit.asset_status_settings ("OrganizationId", "StatusKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_assets_OrganizationId_AssetTag" ON tenebit.assets ("OrganizationId", "AssetTag");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_assets_OrganizationId_Status" ON tenebit.assets ("OrganizationId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_assignments_OrganizationId_ProtocolNumber" ON tenebit.assignments ("OrganizationId", "ProtocolNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_device_trust_tokens_OrganizationUserId_TokenHash" ON tenebit.device_trust_tokens ("OrganizationUserId", "TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_email_verification_tokens_OrganizationUserId" ON tenebit.email_verification_tokens ("OrganizationUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_email_verification_tokens_TokenHash" ON tenebit.email_verification_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_external_logins_Provider_ProviderUserId" ON tenebit.external_logins ("Provider", "ProviderUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_job_profiles_OrganizationId_Name" ON tenebit.job_profiles ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_organization_users_Email" ON tenebit.organization_users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_organization_users_OrganizationId_Email" ON tenebit.organization_users ("OrganizationId", "Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_password_reset_tokens_OrganizationUserId" ON tenebit.password_reset_tokens ("OrganizationUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_password_reset_tokens_TokenHash" ON tenebit.password_reset_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_people_OrganizationId_Email" ON tenebit.people ("OrganizationId", "Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_procedure_acceptances_AssignmentId" ON tenebit.procedure_acceptances ("AssignmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_procedure_documents_OrganizationId_ProcedureId_UploadedAt" ON tenebit.procedure_documents ("OrganizationId", "ProcedureId", "UploadedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_procedure_documents_ProcedureId" ON tenebit.procedure_documents ("ProcedureId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_procedures_OrganizationId_Title_Version" ON tenebit.procedures ("OrganizationId", "Title", "Version");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE INDEX "IX_refresh_tokens_OrganizationUserId" ON tenebit.refresh_tokens ("OrganizationUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_TokenHash" ON tenebit.refresh_tokens ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_sent_alerts_OrganizationId_AlertKey_EntityId" ON tenebit.sent_alerts ("OrganizationId", "AlertKey", "EntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_subscriptions_OrganizationId" ON tenebit.subscriptions ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_teams_OrganizationId_Name" ON tenebit.teams ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    CREATE TABLE tenebit.asset_locations (
        "Id" uuid PRIMARY KEY,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Type" character varying(40) NOT NULL,
        "ParentId" uuid NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL
    );

    CREATE INDEX "IX_asset_locations_OrganizationId_ParentId"
        ON tenebit.asset_locations ("OrganizationId", "ParentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812200749_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260812200749_InitialCreate', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    ALTER TABLE tenebit.asset_status_settings ADD "BackgroundColor" character varying(9) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    ALTER TABLE tenebit.asset_status_settings ADD "Color" character varying(9) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE TABLE tenebit.dashboard_layouts (
        "OrganizationUserId" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "LayoutJson" text NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_dashboard_layouts" PRIMARY KEY ("OrganizationUserId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE TABLE tenebit.dashboard_snapshots (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "SnapshotDate" date NOT NULL,
        "TotalAssets" integer NOT NULL,
        "AssetsWithoutOwner" integer NOT NULL,
        "OpenAssignments" integer NOT NULL,
        "VisibleAssetValue" numeric(18,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_dashboard_snapshots" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE TABLE tenebit.licenses (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Vendor" character varying(160),
        "LicenseKey" character varying(400),
        "SeatsTotal" integer NOT NULL,
        "ExpiresAt" date,
        "Notes" character varying(800),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_licenses" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE TABLE tenebit.person_relation_types (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(80) NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_person_relation_types" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE TABLE tenebit.role_permissions (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "RoleKey" character varying(60) NOT NULL,
        "PermissionKey" character varying(80) NOT NULL,
        "Allowed" boolean NOT NULL,
        CONSTRAINT "PK_role_permissions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE TABLE tenebit.two_factor_recovery_codes (
        "Id" uuid NOT NULL,
        "OrganizationUserId" uuid NOT NULL,
        "CodeHash" character varying(120) NOT NULL,
        "UsedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_two_factor_recovery_codes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE TABLE tenebit.license_seats (
        "LicenseId" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "AssignedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_license_seats" PRIMARY KEY ("LicenseId", "PersonId"),
        CONSTRAINT "FK_license_seats_licenses_LicenseId" FOREIGN KEY ("LicenseId") REFERENCES tenebit.licenses ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE UNIQUE INDEX "IX_dashboard_snapshots_OrganizationId_SnapshotDate" ON tenebit.dashboard_snapshots ("OrganizationId", "SnapshotDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE UNIQUE INDEX "IX_person_relation_types_OrganizationId_Name" ON tenebit.person_relation_types ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE UNIQUE INDEX "IX_role_permissions_OrganizationId_RoleKey_PermissionKey" ON tenebit.role_permissions ("OrganizationId", "RoleKey", "PermissionKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    CREATE INDEX "IX_two_factor_recovery_codes_OrganizationUserId" ON tenebit.two_factor_recovery_codes ("OrganizationUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813062004_SyncModelAfterMerge') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813062004_SyncModelAfterMerge', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063321_AddAssignmentIntegrityFields') THEN
    ALTER TABLE tenebit.procedure_acceptances ADD "ConfirmationHash" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063321_AddAssignmentIntegrityFields') THEN
    ALTER TABLE tenebit.procedure_acceptances ADD "ConfirmedIp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063321_AddAssignmentIntegrityFields') THEN
    ALTER TABLE tenebit.assignments ADD "AcceptanceHash" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063321_AddAssignmentIntegrityFields') THEN
    ALTER TABLE tenebit.assignments ADD "AcceptedIp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063321_AddAssignmentIntegrityFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813063321_AddAssignmentIntegrityFields', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063636_AddStripeSubscriptionFields') THEN
    ALTER TABLE tenebit.subscriptions ADD "StripeCustomerId" character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063636_AddStripeSubscriptionFields') THEN
    ALTER TABLE tenebit.subscriptions ADD "StripeSubscriptionId" character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063636_AddStripeSubscriptionFields') THEN
    CREATE INDEX "IX_subscriptions_StripeCustomerId" ON tenebit.subscriptions ("StripeCustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813063636_AddStripeSubscriptionFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813063636_AddStripeSubscriptionFields', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    ALTER TABLE tenebit.people ADD "DeactivatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    ALTER TABLE tenebit.people ADD "EmploymentEndsAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    ALTER TABLE tenebit.people ADD "EmploymentStatus" character varying(40);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    ALTER TABLE tenebit.people ADD "PreferredLanguage" character varying(8);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    UPDATE tenebit.people SET "EmploymentStatus" = CASE WHEN "IsActive" THEN 'Active' ELSE 'Inactive' END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    ALTER TABLE tenebit.people ALTER COLUMN "EmploymentStatus" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    CREATE INDEX "IX_people_OrganizationId_EmploymentEndsAt" ON tenebit.people ("OrganizationId", "EmploymentEndsAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    CREATE INDEX "IX_people_OrganizationId_EmploymentStatus" ON tenebit.people ("OrganizationId", "EmploymentStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    ALTER TABLE tenebit.people ADD CONSTRAINT "CK_people_employment_status_active" CHECK (("EmploymentStatus" IN ('Active', 'Offboarding') AND "IsActive") OR ("EmploymentStatus" = 'Inactive' AND NOT "IsActive"));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813153647_AddPersonEmploymentLifecycle') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813153647_AddPersonEmploymentLifecycle', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813204056_AddAssignmentAssetPartialReturn') THEN
    ALTER TABLE tenebit.assignment_assets ADD "ReturnLocation" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813204056_AddAssignmentAssetPartialReturn') THEN
    ALTER TABLE tenebit.assignment_assets ADD "ReturnNotes" character varying(800);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813204056_AddAssignmentAssetPartialReturn') THEN
    ALTER TABLE tenebit.assignment_assets ADD "ReturnResolution" character varying(40);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813204056_AddAssignmentAssetPartialReturn') THEN
    ALTER TABLE tenebit.assignment_assets ADD "ReturnedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813204056_AddAssignmentAssetPartialReturn') THEN
    ALTER TABLE tenebit.assignment_assets ADD "ReturnedBy" character varying(240);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813204056_AddAssignmentAssetPartialReturn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813204056_AddAssignmentAssetPartialReturn', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    ALTER TABLE tenebit.asset_categories ADD "PhotoOnIssue" character varying(40) NOT NULL DEFAULT 'Disabled';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    ALTER TABLE tenebit.asset_categories ADD "PhotoOnReturn" character varying(40) NOT NULL DEFAULT 'Disabled';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    ALTER TABLE tenebit.asset_categories ADD "PostReturnDisposition" character varying(40) NOT NULL DEFAULT 'Reuse';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    ALTER TABLE tenebit.asset_categories ADD "ReturnChecklistTemplate" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    ALTER TABLE tenebit.asset_categories ADD "ReturnHandlingMode" character varying(40) NOT NULL DEFAULT 'DirectToStock';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    CREATE TABLE tenebit.asset_inspections (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "AssetId" uuid NOT NULL,
        "AssignmentId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(240),
        "SerialNumberMatched" boolean,
        "AccessoriesComplete" boolean,
        "DataWiped" boolean,
        "FunctionalTestPassed" boolean,
        "DamageAssessmentNotes" character varying(2000),
        "Outcome" character varying(40),
        "Notes" character varying(2000),
        "CompletedAt" timestamp with time zone,
        "CompletedBy" character varying(240),
        CONSTRAINT "PK_asset_inspections" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    CREATE INDEX "IX_asset_inspections_OrganizationId_AssetId_Outcome" ON tenebit.asset_inspections ("OrganizationId", "AssetId", "Outcome");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813210619_AddReturnPolicyAndAssetInspections') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813210619_AddReturnPolicyAndAssetInspections', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214418_AddAssetEvidence') THEN
    CREATE TABLE tenebit.asset_evidence (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "AssetId" uuid NOT NULL,
        "AssignmentId" uuid,
        "OffboardingItemId" uuid,
        "AssetAuditItemId" uuid,
        "Phase" character varying(20) NOT NULL,
        "FileName" character varying(260) NOT NULL,
        "ContentType" character varying(160) NOT NULL,
        "Content" bytea NOT NULL,
        "SizeBytes" bigint NOT NULL,
        "Sha256" character varying(64) NOT NULL,
        "Caption" character varying(500),
        "UploadedAt" timestamp with time zone NOT NULL,
        "UploadedBy" character varying(240) NOT NULL,
        "UploadedVia" character varying(30) NOT NULL,
        "LockedAt" timestamp with time zone,
        CONSTRAINT "PK_asset_evidence" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_asset_evidence_assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES tenebit.assets ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_asset_evidence_assignments_AssignmentId" FOREIGN KEY ("AssignmentId") REFERENCES tenebit.assignments ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214418_AddAssetEvidence') THEN
    CREATE INDEX "IX_asset_evidence_AssetId" ON tenebit.asset_evidence ("AssetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214418_AddAssetEvidence') THEN
    CREATE INDEX "IX_asset_evidence_AssignmentId" ON tenebit.asset_evidence ("AssignmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214418_AddAssetEvidence') THEN
    CREATE INDEX "IX_asset_evidence_OrganizationId_AssetId_Phase" ON tenebit.asset_evidence ("OrganizationId", "AssetId", "Phase");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813214418_AddAssetEvidence') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813214418_AddAssetEvidence', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    DROP INDEX tenebit."IX_sent_alerts_OrganizationId_AlertKey_EntityId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ALTER COLUMN "SentAt" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ADD "AttemptCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ADD "CreatedAt" timestamp with time zone NOT NULL DEFAULT TIMESTAMPTZ '-infinity';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ADD "DigestId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ADD "LastError" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ADD "NextAttemptAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ADD "RecipientEmail" character varying(320) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.sent_alerts ADD "Status" character varying(20) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.organizations ADD "QuietHoursEnd" time without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    ALTER TABLE tenebit.organizations ADD "QuietHoursStart" time without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    CREATE UNIQUE INDEX "IX_sent_alerts_OrganizationId_AlertKey_EntityId_RecipientEmail" ON tenebit.sent_alerts ("OrganizationId", "AlertKey", "EntityId", "RecipientEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814061115_AddSentAlertDeliveryTrackingAndQuietHours') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814061115_AddSentAlertDeliveryTrackingAndQuietHours', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    ALTER TABLE tenebit.organizations ADD "CapturePublicIp" character varying(20) NOT NULL DEFAULT 'Off';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    ALTER TABLE tenebit.organizations ADD "DefaultEvidenceRetentionMonths" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    ALTER TABLE tenebit.organizations ADD "PrivacyContactEmail" character varying(320);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    ALTER TABLE tenebit.organizations ADD "PrivacyNoticeUrl" character varying(600);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    ALTER TABLE tenebit.organizations ADD "PublicIpRetentionDays" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    ALTER TABLE tenebit.asset_evidence ADD "LegalHold" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    ALTER TABLE tenebit.asset_evidence ADD "RedactedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814102642_AddEvidencePrivacyAndRetention') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814102642_AddEvidencePrivacyAndRetention', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814104147_AddOffboardingCaseAndItems') THEN
    CREATE TABLE tenebit.offboarding_cases (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "Status" character varying(20) NOT NULL,
        "EmploymentEndsAt" timestamp with time zone NOT NULL,
        "ReturnDueDate" timestamp with time zone NOT NULL,
        "DefaultReturnLocation" character varying(240),
        "Notes" character varying(2000),
        "ProcessOwnerId" uuid,
        "BlockNewReservations" boolean NOT NULL,
        "CancelFutureReservations" boolean NOT NULL,
        "AutoReleaseLicenses" boolean NOT NULL,
        "PersonDeactivatedAt" timestamp with time zone,
        "ScheduledActionsCompletedAt" timestamp with time zone,
        "PublicTokenHash" character varying(128),
        "PublicTokenExpiresAt" timestamp with time zone,
        "PublicTokenRevokedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(240) NOT NULL,
        "StartedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "CompletedBy" character varying(240),
        "CancelledAt" timestamp with time zone,
        "CancellationReason" character varying(1000),
        "FinalProtocolNumber" character varying(80),
        CONSTRAINT "PK_offboarding_cases" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814104147_AddOffboardingCaseAndItems') THEN
    CREATE TABLE tenebit.offboarding_items (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "OffboardingCaseId" uuid NOT NULL,
        "Type" character varying(30) NOT NULL,
        "AssetId" uuid,
        "AssignmentId" uuid,
        "LicenseId" uuid,
        "Label" character varying(240) NOT NULL,
        "Required" boolean NOT NULL,
        "Status" character varying(30) NOT NULL,
        "EmployeeResponse" character varying(60),
        "EmployeeComment" character varying(1000),
        "AutomationMode" character varying(20) NOT NULL,
        "AutomationLastAttemptAt" timestamp with time zone,
        "AutomationError" character varying(1000),
        "ReceivedAt" timestamp with time zone,
        "ReceivedBy" character varying(240),
        "InspectionCompletedAt" timestamp with time zone,
        "InspectionCompletedBy" character varying(240),
        "ResolutionNotes" character varying(1000),
        "CompletedAt" timestamp with time zone,
        "CompletedBy" character varying(240),
        "SortOrder" integer NOT NULL,
        CONSTRAINT "PK_offboarding_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_offboarding_items_offboarding_cases_OffboardingCaseId" FOREIGN KEY ("OffboardingCaseId") REFERENCES tenebit.offboarding_cases ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814104147_AddOffboardingCaseAndItems') THEN
    CREATE UNIQUE INDEX "IX_offboarding_cases_OrganizationId_PersonId_Open" ON tenebit.offboarding_cases ("OrganizationId", "PersonId") WHERE "Status" NOT IN ('Completed', 'Cancelled');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814104147_AddOffboardingCaseAndItems') THEN
    CREATE INDEX "IX_offboarding_items_OffboardingCaseId" ON tenebit.offboarding_items ("OffboardingCaseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814104147_AddOffboardingCaseAndItems') THEN
    CREATE INDEX "IX_offboarding_items_OrganizationId_OffboardingCaseId" ON tenebit.offboarding_items ("OrganizationId", "OffboardingCaseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814104147_AddOffboardingCaseAndItems') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814104147_AddOffboardingCaseAndItems', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814111728_AddOffboardingItemIdToAssetInspection') THEN
    ALTER TABLE tenebit.asset_inspections ADD "OffboardingItemId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814111728_AddOffboardingItemIdToAssetInspection') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814111728_AddOffboardingItemIdToAssetInspection', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE TABLE tenebit.asset_audit_campaigns (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(240) NOT NULL,
        "Description" character varying(2000),
        "Status" character varying(20) NOT NULL,
        "DueDate" timestamp with time zone NOT NULL,
        "ScopeJson" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(240) NOT NULL,
        "StartedAt" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "CompletedBy" character varying(240),
        CONSTRAINT "PK_asset_audit_campaigns" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE TABLE tenebit.asset_audit_participants (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "CampaignId" uuid NOT NULL,
        "PersonId" uuid NOT NULL,
        "Email" character varying(320) NOT NULL,
        "TokenHash" character varying(128),
        "TokenExpiresAt" timestamp with time zone,
        "TokenRevokedAt" timestamp with time zone,
        "Status" character varying(20) NOT NULL,
        "SubmittedAt" timestamp with time zone,
        "LastReminderAt" timestamp with time zone,
        CONSTRAINT "PK_asset_audit_participants" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_asset_audit_participants_asset_audit_campaigns_CampaignId" FOREIGN KEY ("CampaignId") REFERENCES tenebit.asset_audit_campaigns ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE TABLE tenebit.asset_audit_items (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "CampaignId" uuid NOT NULL,
        "ParticipantId" uuid NOT NULL,
        "AssetId" uuid NOT NULL,
        "ExpectedPersonId" uuid NOT NULL,
        "ExpectedLocation" character varying(240),
        "Response" character varying(20) NOT NULL,
        "Comment" character varying(1000),
        "RespondedAt" timestamp with time zone,
        "Resolution" character varying(30) NOT NULL,
        "ResolutionNotes" character varying(1000),
        "ResolvedAt" timestamp with time zone,
        "ResolvedBy" character varying(240),
        CONSTRAINT "PK_asset_audit_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_asset_audit_items_asset_audit_campaigns_CampaignId" FOREIGN KEY ("CampaignId") REFERENCES tenebit.asset_audit_campaigns ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_asset_audit_items_asset_audit_participants_ParticipantId" FOREIGN KEY ("ParticipantId") REFERENCES tenebit.asset_audit_participants ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE INDEX "IX_asset_audit_campaigns_OrganizationId_Status" ON tenebit.asset_audit_campaigns ("OrganizationId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE INDEX "IX_asset_audit_items_CampaignId" ON tenebit.asset_audit_items ("CampaignId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE INDEX "IX_asset_audit_items_OrganizationId_CampaignId" ON tenebit.asset_audit_items ("OrganizationId", "CampaignId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE INDEX "IX_asset_audit_items_OrganizationId_ParticipantId" ON tenebit.asset_audit_items ("OrganizationId", "ParticipantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE INDEX "IX_asset_audit_items_ParticipantId" ON tenebit.asset_audit_items ("ParticipantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE INDEX "IX_asset_audit_participants_CampaignId" ON tenebit.asset_audit_participants ("CampaignId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    CREATE UNIQUE INDEX "IX_asset_audit_participants_OrganizationId_CampaignId_PersonId" ON tenebit.asset_audit_participants ("OrganizationId", "CampaignId", "PersonId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814121001_AddAssetAuditCampaigns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814121001_AddAssetAuditCampaigns', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.assets ADD "IsReservable" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.assets ADD "MaxReservationDays" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.assets ADD "ReservationInstructions" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.asset_categories ADD "CatalogDescription" character varying(600);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.asset_categories ADD "CatalogImageUrl" character varying(600);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.asset_categories ADD "CatalogName" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.asset_categories ADD "ReservationMode" character varying(40) NOT NULL DEFAULT 'RequestByCategory';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    ALTER TABLE tenebit.asset_categories ADD "VisibleInEmployeeCatalog" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    CREATE TABLE tenebit.equipment_kit_definitions (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(240) NOT NULL,
        "Description" character varying(2000),
        "VisibleInEmployeeCatalog" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" character varying(240) NOT NULL,
        CONSTRAINT "PK_equipment_kit_definitions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    CREATE TABLE tenebit.equipment_kit_definition_items (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "KitDefinitionId" uuid NOT NULL,
        "AssetCategoryId" uuid NOT NULL,
        "RequiredQuantity" integer NOT NULL,
        CONSTRAINT "PK_equipment_kit_definition_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_equipment_kit_definition_items_equipment_kit_definitions_Ki~" FOREIGN KEY ("KitDefinitionId") REFERENCES tenebit.equipment_kit_definitions ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    CREATE INDEX "IX_equipment_kit_definition_items_KitDefinitionId" ON tenebit.equipment_kit_definition_items ("KitDefinitionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    CREATE INDEX "IX_equipment_kit_definition_items_OrganizationId_KitDefinition~" ON tenebit.equipment_kit_definition_items ("OrganizationId", "KitDefinitionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    CREATE UNIQUE INDEX "IX_equipment_kit_definitions_OrganizationId_Name" ON tenebit.equipment_kit_definitions ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814130828_AddReservationCatalogFoundation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814130828_AddReservationCatalogFoundation', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    CREATE TABLE tenebit.equipment_reservations (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "RequesterPersonId" uuid NOT NULL,
        "Status" character varying(20) NOT NULL,
        "StartAt" timestamp with time zone NOT NULL,
        "EndAt" timestamp with time zone NOT NULL,
        "Purpose" character varying(500) NOT NULL,
        "PickupLocation" character varying(240),
        "Notes" character varying(2000),
        "RequestedAt" timestamp with time zone,
        "ApprovedAt" timestamp with time zone,
        "ApprovedBy" character varying(240),
        "RejectedAt" timestamp with time zone,
        "RejectedBy" character varying(240),
        "DecisionNotes" character varying(2000),
        "CancelledAt" timestamp with time zone,
        "CancelledBy" character varying(240),
        "CancellationReason" character varying(2000),
        "AssignmentId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "RowVersion" bytea NOT NULL,
        CONSTRAINT "PK_equipment_reservations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    CREATE TABLE tenebit.equipment_reservation_items (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "ReservationId" uuid NOT NULL,
        "RequestedCategoryId" uuid NOT NULL,
        "RequestedQuantity" integer NOT NULL,
        "KitDefinitionId" uuid,
        "AssetId" uuid,
        "OriginalAssetId" uuid,
        "SubstitutionReason" character varying(1000),
        "Status" character varying(20) NOT NULL,
        CONSTRAINT "PK_equipment_reservation_items" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_equipment_reservation_items_equipment_reservations_Reservat~" FOREIGN KEY ("ReservationId") REFERENCES tenebit.equipment_reservations ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    CREATE INDEX "IX_equipment_reservation_items_OrganizationId_AssetId" ON tenebit.equipment_reservation_items ("OrganizationId", "AssetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    CREATE INDEX "IX_equipment_reservation_items_OrganizationId_ReservationId" ON tenebit.equipment_reservation_items ("OrganizationId", "ReservationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    CREATE INDEX "IX_equipment_reservation_items_ReservationId" ON tenebit.equipment_reservation_items ("ReservationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    CREATE INDEX "IX_equipment_reservations_OrganizationId_RequesterPersonId" ON tenebit.equipment_reservations ("OrganizationId", "RequesterPersonId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    CREATE INDEX "IX_equipment_reservations_OrganizationId_Status_StartAt_EndAt" ON tenebit.equipment_reservations ("OrganizationId", "Status", "StartAt", "EndAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814132320_AddEquipmentReservations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814132320_AddEquipmentReservations', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814153000_AddAssignmentIntegrityVersion') THEN
    ALTER TABLE tenebit.assignments ADD "IntegrityVersion" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814153000_AddAssignmentIntegrityVersion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814153000_AddAssignmentIntegrityVersion', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814161313_AddAlertRulesAndDigestSettings') THEN
    CREATE TABLE tenebit.alert_digest_settings (
        "OrganizationId" uuid NOT NULL,
        "Frequency" character varying(20) NOT NULL,
        "DayOfWeek" character varying(10),
        "LocalTime" time without time zone NOT NULL,
        "QuietHoursStart" time without time zone,
        "QuietHoursEnd" time without time zone,
        "BusinessDays" character varying(40) NOT NULL,
        "HolidayCalendarCountryCode" character varying(8),
        "IncludeEmptyDigest" boolean NOT NULL,
        "LastGeneratedAt" timestamp with time zone,
        CONSTRAINT "PK_alert_digest_settings" PRIMARY KEY ("OrganizationId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814161313_AddAlertRulesAndDigestSettings') THEN
    CREATE TABLE tenebit.alert_rules (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Type" character varying(60) NOT NULL,
        "IsEnabled" boolean NOT NULL,
        "ThresholdDays" integer[] NOT NULL,
        "DeliveryMode" character varying(20) NOT NULL,
        "RecipientMode" character varying(30) NOT NULL,
        "CustomEmails" character varying(600),
        "CooldownDays" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "UpdatedBy" character varying(240) NOT NULL,
        CONSTRAINT "PK_alert_rules" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814161313_AddAlertRulesAndDigestSettings') THEN
    CREATE INDEX "IX_alert_rules_OrganizationId" ON tenebit.alert_rules ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814161313_AddAlertRulesAndDigestSettings') THEN
    CREATE UNIQUE INDEX "IX_alert_rules_OrganizationId_Type" ON tenebit.alert_rules ("OrganizationId", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814161313_AddAlertRulesAndDigestSettings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814161313_AddAlertRulesAndDigestSettings', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814180000_SeedDefaultAlertRules') THEN
    INSERT INTO tenebit.alert_rules ("Id", "OrganizationId", "Type", "IsEnabled", "ThresholdDays", "DeliveryMode", "RecipientMode", "CustomEmails", "CooldownDays", "CreatedAt", "UpdatedAt", "UpdatedBy")
    SELECT gen_random_uuid(), o."Id", d."Type", d."IsEnabled", d."ThresholdDays", 'Immediate', 'OwnersAndAdmins', NULL, 1, now(), now(), 'system'
    FROM tenebit.organizations o
    CROSS JOIN (VALUES
        ('AssetWarrantyExpiring', true, ARRAY[30,7]::integer[]),
        ('AssignmentReturnDue', true, ARRAY[0]::integer[]),
        ('AssignmentNotConfirmed', true, ARRAY[0]::integer[]),
        ('LicenseExpiring', false, ARRAY[30,7]::integer[]),
        ('ProcedureReviewDue', false, ARRAY[30,7]::integer[]),
        ('OffboardingReturnDue', false, ARRAY[7]::integer[]),
        ('AssetAuditNoResponse', false, ARRAY[7]::integer[]),
        ('ReservationAwaitingApproval', false, ARRAY[1]::integer[]),
        ('ReservationPickupUpcoming', false, ARRAY[1]::integer[]),
        ('ReservationOverdue', false, ARRAY[0]::integer[])
    ) AS d("Type", "IsEnabled", "ThresholdDays")
    WHERE NOT EXISTS (
        SELECT 1 FROM tenebit.alert_rules r
        WHERE r."OrganizationId" = o."Id" AND r."Type" = d."Type"
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814180000_SeedDefaultAlertRules') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814180000_SeedDefaultAlertRules', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815125601_AddServiceTickets') THEN
    CREATE TABLE tenebit.service_tickets (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "AssetId" uuid NOT NULL,
        "AssetInspectionId" uuid,
        "Vendor" character varying(200) NOT NULL,
        "Description" character varying(2000),
        "EstimatedCost" numeric,
        "ActualCost" numeric,
        "Currency" character varying(3),
        "OpenedAt" timestamp with time zone NOT NULL,
        "SlaDueAt" timestamp with time zone,
        "ClosedAt" timestamp with time zone,
        "Status" character varying(30) NOT NULL,
        "Resolution" character varying(2000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_service_tickets" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815125601_AddServiceTickets') THEN
    CREATE INDEX "IX_service_tickets_OrganizationId_AssetId_Status" ON tenebit.service_tickets ("OrganizationId", "AssetId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815125601_AddServiceTickets') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260815125601_AddServiceTickets', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816141723_AddQrLabelSettings') THEN
    ALTER TABLE tenebit.organizations ADD "QrLabelShowName" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816141723_AddQrLabelSettings') THEN
    ALTER TABLE tenebit.organizations ADD "QrLabelShowTag" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816141723_AddQrLabelSettings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816141723_AddQrLabelSettings', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816204908_AddAssignmentPublicToken') THEN
    ALTER TABLE tenebit.assignments ADD "PublicTokenExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816204908_AddAssignmentPublicToken') THEN
    ALTER TABLE tenebit.assignments ADD "PublicTokenHash" character varying(128);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816204908_AddAssignmentPublicToken') THEN
    ALTER TABLE tenebit.assignments ADD "PublicTokenRevokedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816204908_AddAssignmentPublicToken') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816204908_AddAssignmentPublicToken', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_items DROP CONSTRAINT "FK_asset_audit_items_asset_audit_campaigns_CampaignId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_items DROP CONSTRAINT "FK_asset_audit_items_asset_audit_participants_ParticipantId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_participants DROP CONSTRAINT "FK_asset_audit_participants_asset_audit_campaigns_CampaignId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_evidence DROP CONSTRAINT "FK_asset_evidence_assets_AssetId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_evidence DROP CONSTRAINT "FK_asset_evidence_assignments_AssignmentId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.equipment_reservation_items DROP CONSTRAINT "FK_equipment_reservation_items_equipment_reservations_Reservat~";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.offboarding_items DROP CONSTRAINT "FK_offboarding_items_offboarding_cases_OffboardingCaseId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    DROP INDEX tenebit."IX_offboarding_items_OffboardingCaseId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    DROP INDEX tenebit."IX_equipment_reservation_items_ReservationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    DROP INDEX tenebit."IX_asset_evidence_AssetId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    DROP INDEX tenebit."IX_asset_evidence_AssignmentId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    DROP INDEX tenebit."IX_asset_audit_participants_CampaignId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    DROP INDEX tenebit."IX_asset_audit_items_CampaignId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    DROP INDEX tenebit."IX_asset_audit_items_ParticipantId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.offboarding_cases ADD CONSTRAINT "AK_offboarding_cases_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.equipment_reservations ADD CONSTRAINT "AK_equipment_reservations_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.assignments ADD CONSTRAINT "AK_assignments_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.assets ADD CONSTRAINT "AK_assets_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_participants ADD CONSTRAINT "AK_asset_audit_participants_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_campaigns ADD CONSTRAINT "AK_asset_audit_campaigns_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    CREATE INDEX "IX_asset_evidence_OrganizationId_AssignmentId" ON tenebit.asset_evidence ("OrganizationId", "AssignmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_items ADD CONSTRAINT "FK_asset_audit_items_asset_audit_campaigns_OrganizationId_Camp~" FOREIGN KEY ("OrganizationId", "CampaignId") REFERENCES tenebit.asset_audit_campaigns ("OrganizationId", "Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_items ADD CONSTRAINT "FK_asset_audit_items_asset_audit_participants_OrganizationId_P~" FOREIGN KEY ("OrganizationId", "ParticipantId") REFERENCES tenebit.asset_audit_participants ("OrganizationId", "Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_audit_participants ADD CONSTRAINT "FK_asset_audit_participants_asset_audit_campaigns_Organization~" FOREIGN KEY ("OrganizationId", "CampaignId") REFERENCES tenebit.asset_audit_campaigns ("OrganizationId", "Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_evidence ADD CONSTRAINT "FK_asset_evidence_assets_OrganizationId_AssetId" FOREIGN KEY ("OrganizationId", "AssetId") REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.asset_evidence ADD CONSTRAINT "FK_asset_evidence_assignments_OrganizationId_AssignmentId" FOREIGN KEY ("OrganizationId", "AssignmentId") REFERENCES tenebit.assignments ("OrganizationId", "Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.equipment_reservation_items ADD CONSTRAINT "FK_equipment_reservation_items_equipment_reservations_Organiza~" FOREIGN KEY ("OrganizationId", "ReservationId") REFERENCES tenebit.equipment_reservations ("OrganizationId", "Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    ALTER TABLE tenebit.offboarding_items ADD CONSTRAINT "FK_offboarding_items_offboarding_cases_OrganizationId_Offboard~" FOREIGN KEY ("OrganizationId", "OffboardingCaseId") REFERENCES tenebit.offboarding_cases ("OrganizationId", "Id") ON DELETE CASCADE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817062322_AddTenantCompositeForeignKeys') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817062322_AddTenantCompositeForeignKeys', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817084613_AddPublicTokenHashIndexes') THEN
    CREATE UNIQUE INDEX "IX_offboarding_cases_PublicTokenHash" ON tenebit.offboarding_cases ("PublicTokenHash") WHERE "PublicTokenHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817084613_AddPublicTokenHashIndexes') THEN
    CREATE UNIQUE INDEX "IX_assignments_PublicTokenHash" ON tenebit.assignments ("PublicTokenHash") WHERE "PublicTokenHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817084613_AddPublicTokenHashIndexes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817084613_AddPublicTokenHashIndexes', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817090221_AddDeviceTrustTokenRevocation') THEN
    ALTER TABLE tenebit.device_trust_tokens ADD "RevokedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817090221_AddDeviceTrustTokenRevocation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817090221_AddDeviceTrustTokenRevocation', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091513_AddStripeWebhookIdempotency') THEN
    ALTER TABLE tenebit.subscriptions ADD "LastWebhookEventAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091513_AddStripeWebhookIdempotency') THEN
    CREATE TABLE tenebit.processed_stripe_events (
        "Id" uuid NOT NULL,
        "EventId" character varying(120) NOT NULL,
        "ProcessedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_processed_stripe_events" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091513_AddStripeWebhookIdempotency') THEN
    CREATE UNIQUE INDEX "IX_processed_stripe_events_EventId" ON tenebit.processed_stripe_events ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091513_AddStripeWebhookIdempotency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817091513_AddStripeWebhookIdempotency', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.teams ADD CONSTRAINT "AK_teams_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.people ADD CONSTRAINT "AK_people_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.asset_inspections ADD CONSTRAINT "AK_asset_inspections_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_teams_OrganizationId_ManagerId" ON tenebit.teams ("OrganizationId", "ManagerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_service_tickets_OrganizationId_AssetInspectionId" ON tenebit.service_tickets ("OrganizationId", "AssetInspectionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_people_OrganizationId_ManagerId" ON tenebit.people ("OrganizationId", "ManagerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_people_OrganizationId_TeamId" ON tenebit.people ("OrganizationId", "TeamId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_offboarding_cases_OrganizationId_ProcessOwnerId" ON tenebit.offboarding_cases ("OrganizationId", "ProcessOwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_job_profiles_OrganizationId_DefaultManagerId" ON tenebit.job_profiles ("OrganizationId", "DefaultManagerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_assets_OrganizationId_AssignedPersonId" ON tenebit.assets ("OrganizationId", "AssignedPersonId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    CREATE INDEX "IX_assets_OrganizationId_TeamId" ON tenebit.assets ("OrganizationId", "TeamId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.assets ADD CONSTRAINT "FK_assets_people_OrganizationId_AssignedPersonId" FOREIGN KEY ("OrganizationId", "AssignedPersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.assets ADD CONSTRAINT "FK_assets_teams_OrganizationId_TeamId" FOREIGN KEY ("OrganizationId", "TeamId") REFERENCES tenebit.teams ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.job_profiles ADD CONSTRAINT "FK_job_profiles_people_OrganizationId_DefaultManagerId" FOREIGN KEY ("OrganizationId", "DefaultManagerId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.offboarding_cases ADD CONSTRAINT "FK_offboarding_cases_people_OrganizationId_ProcessOwnerId" FOREIGN KEY ("OrganizationId", "ProcessOwnerId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.people ADD CONSTRAINT "FK_people_people_OrganizationId_ManagerId" FOREIGN KEY ("OrganizationId", "ManagerId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.people ADD CONSTRAINT "FK_people_teams_OrganizationId_TeamId" FOREIGN KEY ("OrganizationId", "TeamId") REFERENCES tenebit.teams ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.service_tickets ADD CONSTRAINT "FK_service_tickets_asset_inspections_OrganizationId_AssetInspe~" FOREIGN KEY ("OrganizationId", "AssetInspectionId") REFERENCES tenebit.asset_inspections ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    ALTER TABLE tenebit.teams ADD CONSTRAINT "FK_teams_people_OrganizationId_ManagerId" FOREIGN KEY ("OrganizationId", "ManagerId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817091726_AddTenantCompositeForeignKeysP0') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817091726_AddTenantCompositeForeignKeysP0', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817101658_AddFieldEncryptionColumnSizing') THEN
    ALTER TABLE tenebit.organization_users ALTER COLUMN "TotpSecret" TYPE character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817101658_AddFieldEncryptionColumnSizing') THEN
    ALTER TABLE tenebit.licenses ALTER COLUMN "LicenseKey" TYPE character varying(600);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817101658_AddFieldEncryptionColumnSizing') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817101658_AddFieldEncryptionColumnSizing', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817123926_AddLocationEntityMapping') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817123926_AddLocationEntityMapping', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817172359_AddAssetAuditParticipantTokenHashIndex') THEN
    CREATE UNIQUE INDEX "IX_asset_audit_participants_TokenHash" ON tenebit.asset_audit_participants ("TokenHash") WHERE "TokenHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817172359_AddAssetAuditParticipantTokenHashIndex') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817172359_AddAssetAuditParticipantTokenHashIndex', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817184500_AddOrganizationUserSecurityStamp') THEN
    ALTER TABLE tenebit.organization_users ADD "SecurityStamp" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817184500_AddOrganizationUserSecurityStamp') THEN
    UPDATE tenebit.organization_users
    SET "SecurityStamp" = "Id"
    WHERE "SecurityStamp" = '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817184500_AddOrganizationUserSecurityStamp') THEN
    ALTER TABLE tenebit.organization_users ALTER COLUMN "SecurityStamp" TYPE uuid;
    ALTER TABLE tenebit.organization_users ALTER COLUMN "SecurityStamp" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817184500_AddOrganizationUserSecurityStamp') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817184500_AddOrganizationUserSecurityStamp', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817201500_AddOrganizationUserPersonLink') THEN
    ALTER TABLE tenebit.organization_users ADD "PersonId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817201500_AddOrganizationUserPersonLink') THEN
    UPDATE tenebit.organization_users AS u
    SET "PersonId" = p."Id"
    FROM tenebit.people AS p
    WHERE p."OrganizationId" = u."OrganizationId"
      AND lower(p."Email") = lower(u."Email")
      AND u."PersonId" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817201500_AddOrganizationUserPersonLink') THEN
    CREATE UNIQUE INDEX "IX_organization_users_OrganizationId_PersonId" ON tenebit.organization_users ("OrganizationId", "PersonId") WHERE "PersonId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817201500_AddOrganizationUserPersonLink') THEN
    ALTER TABLE tenebit.organization_users ADD CONSTRAINT "FK_organization_users_people_OrganizationId_PersonId" FOREIGN KEY ("OrganizationId", "PersonId") REFERENCES tenebit.people ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817201500_AddOrganizationUserPersonLink') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817201500_AddOrganizationUserPersonLink', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817214500_CompleteTenantCompositeForeignKeys') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817214500_CompleteTenantCompositeForeignKeys') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817214500_CompleteTenantCompositeForeignKeys') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817214500_CompleteTenantCompositeForeignKeys') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817214500_CompleteTenantCompositeForeignKeys') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817214500_CompleteTenantCompositeForeignKeys', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.refresh_tokens ADD "FamilyId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.refresh_tokens ADD "ParentTokenId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.refresh_tokens ADD "ReplacedByTokenId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    UPDATE tenebit.refresh_tokens SET "FamilyId" = "Id" WHERE "FamilyId" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.refresh_tokens ALTER COLUMN "FamilyId" TYPE uuid;
    ALTER TABLE tenebit.refresh_tokens ALTER COLUMN "FamilyId" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_refresh_tokens_FamilyId" ON tenebit.refresh_tokens ("FamilyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_refresh_tokens_ParentTokenId" ON tenebit.refresh_tokens ("ParentTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_refresh_tokens_ReplacedByTokenId" ON tenebit.refresh_tokens ("ReplacedByTokenId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.refresh_tokens ADD CONSTRAINT "FK_refresh_tokens_refresh_tokens_ParentTokenId" FOREIGN KEY ("ParentTokenId") REFERENCES tenebit.refresh_tokens ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.refresh_tokens ADD CONSTRAINT "FK_refresh_tokens_refresh_tokens_ReplacedByTokenId" FOREIGN KEY ("ReplacedByTokenId") REFERENCES tenebit.refresh_tokens ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.licenses ALTER COLUMN "LicenseKey" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.asset_field_values ALTER COLUMN "Value" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE TABLE tenebit.background_job_runs (
        "JobName" character varying(120) NOT NULL,
        "LastRunAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_background_job_runs" PRIMARY KEY ("JobName")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE TABLE tenebit.oauth_transactions (
        "Id" uuid NOT NULL,
        "StateHash" character varying(128) NOT NULL,
        "Provider" character varying(40) NOT NULL,
        "CodeVerifier" character varying(160) NOT NULL,
        "ReturnPath" character varying(1024) NOT NULL,
        "CorrelationHash" character varying(128) NOT NULL,
        "Nonce" character varying(160) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "ConsumedAt" timestamp with time zone,
        CONSTRAINT "PK_oauth_transactions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE UNIQUE INDEX "IX_oauth_transactions_StateHash" ON tenebit.oauth_transactions ("StateHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_oauth_transactions_ExpiresAt" ON tenebit.oauth_transactions ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE TABLE tenebit.two_factor_challenges (
        "Id" uuid NOT NULL,
        "TicketHash" character varying(128) NOT NULL,
        "OrganizationUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "ConsumedAt" timestamp with time zone,
        CONSTRAINT "PK_two_factor_challenges" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_two_factor_challenges_organization_users_OrganizationUserId" FOREIGN KEY ("OrganizationUserId") REFERENCES tenebit.organization_users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE UNIQUE INDEX "IX_two_factor_challenges_TicketHash" ON tenebit.two_factor_challenges ("TicketHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_two_factor_challenges_ExpiresAt" ON tenebit.two_factor_challenges ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_two_factor_challenges_OrganizationUserId" ON tenebit.two_factor_challenges ("OrganizationUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.asset_locations ADD "NormalizedName" character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    UPDATE tenebit.asset_locations SET "NormalizedName" = upper(trim("Name"));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    DO $$
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM tenebit.asset_locations
            GROUP BY "OrganizationId", "ParentId", "NormalizedName"
            HAVING count(*) > 1
        ) THEN
            RAISE EXCEPTION 'AUD3-019 preflight: duplicate sibling location names must be resolved before migration.';
        END IF;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.asset_locations ALTER COLUMN "NormalizedName" TYPE character varying(120);
    ALTER TABLE tenebit.asset_locations ALTER COLUMN "NormalizedName" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE UNIQUE INDEX "UX_asset_locations_sibling_name" ON tenebit.asset_locations ("OrganizationId", "ParentId", "NormalizedName") WHERE "ParentId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE UNIQUE INDEX "UX_asset_locations_root_name" ON tenebit.asset_locations ("OrganizationId", "NormalizedName") WHERE "ParentId" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.assets ADD "LocationId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.people ADD "LocationId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    UPDATE tenebit.assets SET "Location" = NULL WHERE "Location" IS NOT NULL AND btrim("Location") = ''; 
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    UPDATE tenebit.people SET "Location" = NULL WHERE "Location" IS NOT NULL AND btrim("Location") = ''; 
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    WITH RECURSIVE location_paths AS (
        SELECT l."Id", l."OrganizationId", l."ParentId", l."Name"::text AS full_path
        FROM tenebit.asset_locations l
        WHERE l."ParentId" IS NULL
        UNION ALL
        SELECT c."Id", c."OrganizationId", c."ParentId", (p.full_path || ' / ' || c."Name")::text
        FROM tenebit.asset_locations c
        JOIN location_paths p ON p."Id" = c."ParentId" AND p."OrganizationId" = c."OrganizationId"
    )
    UPDATE tenebit.assets a
    SET "LocationId" = p."Id", "Location" = p.full_path
    FROM location_paths p
    WHERE a."OrganizationId" = p."OrganizationId"
      AND a."Location" IS NOT NULL
      AND lower(trim(a."Location")) = lower(p.full_path);

    WITH RECURSIVE location_paths AS (
        SELECT l."Id", l."OrganizationId", l."ParentId", l."Name"::text AS full_path
        FROM tenebit.asset_locations l
        WHERE l."ParentId" IS NULL
        UNION ALL
        SELECT c."Id", c."OrganizationId", c."ParentId", (p.full_path || ' / ' || c."Name")::text
        FROM tenebit.asset_locations c
        JOIN location_paths p ON p."Id" = c."ParentId" AND p."OrganizationId" = c."OrganizationId"
    )
    UPDATE tenebit.people pe
    SET "LocationId" = p."Id", "Location" = p.full_path
    FROM location_paths p
    WHERE pe."OrganizationId" = p."OrganizationId"
      AND pe."Location" IS NOT NULL
      AND lower(trim(pe."Location")) = lower(p.full_path);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    DO $$
    BEGIN
        IF EXISTS (SELECT 1 FROM tenebit.assets WHERE "Location" IS NOT NULL AND "LocationId" IS NULL) THEN
            RAISE EXCEPTION 'AUD3-019 preflight: at least one asset has a legacy location path that does not resolve to asset_locations.';
        END IF;
        IF EXISTS (SELECT 1 FROM tenebit.people WHERE "Location" IS NOT NULL AND "LocationId" IS NULL) THEN
            RAISE EXCEPTION 'AUD3-019 preflight: at least one person has a legacy location path that does not resolve to asset_locations.';
        END IF;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_assets_OrganizationId_LocationId" ON tenebit.assets ("OrganizationId", "LocationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    CREATE INDEX "IX_people_OrganizationId_LocationId" ON tenebit.people ("OrganizationId", "LocationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.assets ADD CONSTRAINT "FK_assets_asset_locations_OrganizationId_LocationId" FOREIGN KEY ("OrganizationId", "LocationId") REFERENCES tenebit.asset_locations ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    ALTER TABLE tenebit.people ADD CONSTRAINT "FK_people_asset_locations_OrganizationId_LocationId" FOREIGN KEY ("OrganizationId", "LocationId") REFERENCES tenebit.asset_locations ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818004500_SecurityStateAndNormalizedLocations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818004500_SecurityStateAndNormalizedLocations', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818023000_CompleteRefreshTokenFamilyMetadata') THEN
    ALTER TABLE tenebit.refresh_tokens ADD "RevocationReason" character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818023000_CompleteRefreshTokenFamilyMetadata') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818023000_CompleteRefreshTokenFamilyMetadata', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    ALTER TABLE tenebit.activity_logs ADD "SourceIp" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    ALTER TABLE tenebit.activity_logs ADD "SourceIpExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    CREATE INDEX "IX_activity_logs_SourceIpExpiresAt" ON tenebit.activity_logs ("SourceIpExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    ALTER TABLE tenebit.procedure_acceptances ADD "IntegrityVersion" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    ALTER TABLE tenebit.subscriptions ADD "CheckoutAttemptId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    ALTER TABLE tenebit.subscriptions ADD "CheckoutAttemptExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    CREATE TABLE tenebit.auth_rate_limit_buckets (
        "KeyHash" character varying(64) NOT NULL,
        "BucketStart" timestamp with time zone NOT NULL,
        "Count" integer NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_auth_rate_limit_buckets" PRIMARY KEY ("KeyHash", "BucketStart")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    CREATE INDEX "IX_auth_rate_limit_buckets_ExpiresAt" ON tenebit.auth_rate_limit_buckets ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    UPDATE tenebit.activity_logs
    SET "ActorSubject" = 'public-scan'
    WHERE "ActorSubject" LIKE 'public-scan:%';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    UPDATE tenebit.assignments
    SET "PublicTokenRevokedAt" = NOW()
    WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

    UPDATE tenebit.offboarding_cases
    SET "PublicTokenRevokedAt" = NOW()
    WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

    UPDATE tenebit.asset_audit_participants
    SET "TokenRevokedAt" = NOW()
    WHERE "TokenHash" IS NOT NULL AND "TokenRevokedAt" IS NULL;

    UPDATE tenebit.password_reset_tokens
    SET "UsedAt" = NOW()
    WHERE "UsedAt" IS NULL;

    UPDATE tenebit.email_verification_tokens
    SET "UsedAt" = NOW()
    WHERE "UsedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818093000_Audit9CriticalClosure') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818093000_Audit9CriticalClosure', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818143000_Audit11RegressionCredentialRevocation') THEN
    UPDATE tenebit.assignments
    SET "PublicTokenRevokedAt" = NOW()
    WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

    UPDATE tenebit.offboarding_cases
    SET "PublicTokenRevokedAt" = NOW()
    WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

    UPDATE tenebit.asset_audit_participants
    SET "TokenRevokedAt" = NOW()
    WHERE "TokenHash" IS NOT NULL AND "TokenRevokedAt" IS NULL;

    UPDATE tenebit.password_reset_tokens
    SET "UsedAt" = NOW()
    WHERE "UsedAt" IS NULL;

    UPDATE tenebit.email_verification_tokens
    SET "UsedAt" = NOW()
    WHERE "UsedAt" IS NULL;

    UPDATE tenebit.activity_logs
    SET "ActorSubject" = 'public-scan'
    WHERE "ActorSubject" LIKE 'public-scan:%';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818143000_Audit11RegressionCredentialRevocation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818143000_Audit11RegressionCredentialRevocation', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818162000_TransactionalEmailOutbox') THEN
    CREATE TABLE tenebit.email_outbox_messages (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "RecipientCiphertext" text NOT NULL,
        "SubjectCiphertext" text NOT NULL,
        "HtmlCiphertext" text NOT NULL,
        "Purpose" character varying(80) NOT NULL,
        "IdempotencyKey" character varying(160) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "AttemptCount" integer NOT NULL DEFAULT 0,
        "NextAttemptAt" timestamp with time zone,
        "LeaseId" uuid,
        "LeaseUntil" timestamp with time zone,
        "SentAt" timestamp with time zone,
        "LastError" character varying(80),
        CONSTRAINT "PK_email_outbox_messages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_email_outbox_messages_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES tenebit.organizations ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818162000_TransactionalEmailOutbox') THEN
    ALTER TABLE tenebit.email_outbox_messages ADD CONSTRAINT "CK_email_outbox_messages_AttemptCount" CHECK ("AttemptCount" >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818162000_TransactionalEmailOutbox') THEN
    CREATE UNIQUE INDEX "IX_email_outbox_messages_OrganizationId_IdempotencyKey" ON tenebit.email_outbox_messages ("OrganizationId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818162000_TransactionalEmailOutbox') THEN
    CREATE INDEX "IX_email_outbox_messages_dispatch" ON tenebit.email_outbox_messages ("SentAt", "NextAttemptAt", "LeaseUntil", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818162000_TransactionalEmailOutbox') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818162000_TransactionalEmailOutbox', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818181000_Audit9CapabilityIncidentFinalClosure') THEN
    UPDATE tenebit.assignments
    SET "PublicTokenRevokedAt" = NOW()
    WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

    UPDATE tenebit.offboarding_cases
    SET "PublicTokenRevokedAt" = NOW()
    WHERE "PublicTokenHash" IS NOT NULL AND "PublicTokenRevokedAt" IS NULL;

    UPDATE tenebit.asset_audit_participants
    SET "TokenRevokedAt" = NOW()
    WHERE "TokenHash" IS NOT NULL AND "TokenRevokedAt" IS NULL;

    UPDATE tenebit.password_reset_tokens
    SET "UsedAt" = NOW()
    WHERE "UsedAt" IS NULL;

    UPDATE tenebit.email_verification_tokens
    SET "UsedAt" = NOW()
    WHERE "UsedAt" IS NULL;

    UPDATE tenebit.activity_logs
    SET "ActorSubject" = 'public-scan'
    WHERE "ActorSubject" LIKE 'public-scan:%';

    -- A pending security e-mail can contain the raw credential that has just been revoked above.
    -- Quarantine it instead of delivering a known-dead link after the deployment. The payload is
    -- erased and AttemptCount=8 keeps the dispatcher from reclaiming the row.
    UPDATE tenebit.email_outbox_messages
    SET "AttemptCount" = 8,
        "NextAttemptAt" = NULL,
        "LeaseId" = NULL,
        "LeaseUntil" = NULL,
        "LastError" = 'incident_credential_revoked',
        "RecipientCiphertext" = '',
        "SubjectCiphertext" = '',
        "HtmlCiphertext" = ''
    WHERE "SentAt" IS NULL
      AND "Purpose" IN (
          'assignment-acceptance',
          'offboarding-public-link',
          'asset-audit-public-link',
          'asset-audit-reminder',
          'password-reset',
          'email-verification',
          'organization-invitation'
      );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818181000_Audit9CapabilityIncidentFinalClosure') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818181000_Audit9CapabilityIncidentFinalClosure', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    ALTER TABLE tenebit.organization_users ADD "LastUsedTotpCounter" bigint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
    END IF;
END $EF$;
COMMIT;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_assets_Name_trgm" ON tenebit.assets USING gin (lower("Name") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_assets_AssetTag_trgm" ON tenebit.assets USING gin (lower("AssetTag") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_assets_SerialNumber_trgm" ON tenebit.assets USING gin (lower("SerialNumber") gin_trgm_ops) WHERE "SerialNumber" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_procedures_Title_trgm" ON tenebit.procedures USING gin (lower("Title") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_procedures_Owner_trgm" ON tenebit.procedures USING gin (lower("Owner") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_procedures_Version_trgm" ON tenebit.procedures USING gin (lower("Version") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_people_FirstName_trgm" ON tenebit.people USING gin (lower("FirstName") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_people_LastName_trgm" ON tenebit.people USING gin (lower("LastName") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_people_Email_trgm" ON tenebit.people USING gin (lower("Email") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_people_FullName_trgm" ON tenebit.people USING gin (lower("FirstName" || ' ' || "LastName") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_assignments_ProtocolNumber_trgm" ON tenebit.assignments USING gin (lower("ProtocolNumber") gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_activity_logs_Action_trgm" ON tenebit.activity_logs USING gin ("Action" gin_trgm_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_activity_logs_Details_trgm" ON tenebit.activity_logs USING gin ("Details" gin_trgm_ops) WHERE "Details" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_activity_logs_recent_entity_actor_action" ON tenebit.activity_logs ("OrganizationId", "EntityType", "EntityId", "ActorSubject", "Action", "CreatedAt" DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_asset_evidence_retention_due" ON tenebit.asset_evidence ("OrganizationId", "UploadedAt") WHERE "LegalHold" = FALSE AND "RedactedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_assets_warranty_due" ON tenebit.assets ("OrganizationId", "WarrantyUntil") WHERE "WarrantyUntil" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818193000_AuditPerformanceSecurityHardening') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818193000_AuditPerformanceSecurityHardening', '10.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818204500_AddActivityLogRetentionIndex') THEN
    CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_activity_logs_CreatedAt" ON tenebit.activity_logs ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818204500_AddActivityLogRetentionIndex') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818204500_AddActivityLogRetentionIndex', '10.0.4');
    END IF;
END $EF$;
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824090000_AdminPanelLoginHistoryAndModeration') THEN
    CREATE TABLE IF NOT EXISTS tenebit.login_events (
        "Id" uuid NOT NULL CONSTRAINT "PK_login_events" PRIMARY KEY,
        "OrganizationId" uuid NULL,
        "UserId" uuid NULL,
        "Email" character varying(320) NOT NULL,
        "Succeeded" boolean NOT NULL,
        "FailureReason" character varying(64) NULL,
        "IpAddress" character varying(64) NULL,
        "UserAgent" character varying(400) NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "IpExpiresAt" timestamp with time zone NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824090000_AdminPanelLoginHistoryAndModeration') THEN
    CREATE INDEX IF NOT EXISTS "IX_login_events_CreatedAt"
        ON tenebit.login_events ("CreatedAt");
    CREATE INDEX IF NOT EXISTS "IX_login_events_OrganizationId_CreatedAt"
        ON tenebit.login_events ("OrganizationId", "CreatedAt");
    CREATE INDEX IF NOT EXISTS "IX_login_events_Email_CreatedAt"
        ON tenebit.login_events ("Email", "CreatedAt");
    CREATE INDEX IF NOT EXISTS "IX_login_events_IpExpiresAt"
        ON tenebit.login_events ("IpExpiresAt") WHERE "IpExpiresAt" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824090000_AdminPanelLoginHistoryAndModeration') THEN
    CREATE TABLE IF NOT EXISTS tenebit.admin_audit_logs (
        "Id" uuid NOT NULL CONSTRAINT "PK_admin_audit_logs" PRIMARY KEY,
        "Action" character varying(80) NOT NULL,
        "TargetType" character varying(40) NULL,
        "TargetId" uuid NULL,
        "TargetLabel" character varying(240) NULL,
        "Details" character varying(1000) NULL,
        "IpAddress" character varying(64) NULL,
        "CreatedAt" timestamp with time zone NOT NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824090000_AdminPanelLoginHistoryAndModeration') THEN
    CREATE INDEX IF NOT EXISTS "IX_admin_audit_logs_CreatedAt"
        ON tenebit.admin_audit_logs ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824090000_AdminPanelLoginHistoryAndModeration') THEN
    ALTER TABLE tenebit.organizations
        ADD COLUMN IF NOT EXISTS "IsSuspended" boolean NOT NULL DEFAULT FALSE,
        ADD COLUMN IF NOT EXISTS "SuspendedAt" timestamp with time zone NULL,
        ADD COLUMN IF NOT EXISTS "SuspendedReason" character varying(500) NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824090000_AdminPanelLoginHistoryAndModeration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260824090000_AdminPanelLoginHistoryAndModeration', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824160000_AssetCategoryDepreciation') THEN
    ALTER TABLE tenebit.asset_categories
        ADD COLUMN IF NOT EXISTS "DepreciationMonths" integer NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260824160000_AssetCategoryDepreciation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260824160000_AssetCategoryDepreciation', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090000_MaintenanceSchedules') THEN
    CREATE TABLE IF NOT EXISTS tenebit.maintenance_schedules (
        "Id" uuid NOT NULL CONSTRAINT "PK_maintenance_schedules" PRIMARY KEY,
        "OrganizationId" uuid NOT NULL,
        "AssetId" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "IntervalMonths" integer NOT NULL,
        "NextDueOn" date NOT NULL,
        "LastPerformedOn" date NULL,
        "LastPerformedBy" character varying(240) NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "AK_maintenance_schedules_OrganizationId_Id" UNIQUE ("OrganizationId", "Id"),
        CONSTRAINT "FK_maintenance_schedules_assets_OrganizationId_AssetId"
            FOREIGN KEY ("OrganizationId", "AssetId")
            REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090000_MaintenanceSchedules') THEN
    CREATE INDEX IF NOT EXISTS "IX_maintenance_schedules_OrganizationId_NextDueOn"
        ON tenebit.maintenance_schedules ("OrganizationId", "NextDueOn");
    CREATE INDEX IF NOT EXISTS "IX_maintenance_schedules_OrganizationId_AssetId"
        ON tenebit.maintenance_schedules ("OrganizationId", "AssetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825090000_MaintenanceSchedules') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260825090000_MaintenanceSchedules', '10.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260826120000_PublicReportThrottle') THEN
    CREATE TABLE IF NOT EXISTS tenebit.public_report_throttle (
        "Id" uuid NOT NULL CONSTRAINT "PK_public_report_throttle" PRIMARY KEY,
        "OrganizationId" uuid NOT NULL,
        "AssetId" uuid NOT NULL,
        "ReporterHash" character varying(64) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "FK_public_report_throttle_assets_OrganizationId_AssetId"
            FOREIGN KEY ("OrganizationId", "AssetId")
            REFERENCES tenebit.assets ("OrganizationId", "Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260826120000_PublicReportThrottle') THEN
    CREATE INDEX IF NOT EXISTS "IX_public_report_throttle_OrganizationId_AssetId_CreatedAt"
        ON tenebit.public_report_throttle ("OrganizationId", "AssetId", "CreatedAt");
    CREATE INDEX IF NOT EXISTS "IX_public_report_throttle_OrganizationId_ReporterHash_CreatedAt"
        ON tenebit.public_report_throttle ("OrganizationId", "ReporterHash", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260826120000_PublicReportThrottle') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260826120000_PublicReportThrottle', '10.0.4');
    END IF;
END $EF$;
COMMIT;

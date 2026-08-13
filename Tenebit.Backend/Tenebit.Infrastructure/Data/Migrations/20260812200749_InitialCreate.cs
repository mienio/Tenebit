using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenebit");

            migrationBuilder.CreateTable(
                name: "activity_logs",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorSubject = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "asset_categories",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "asset_status_settings",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_status_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    AssetTag = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AssignedPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WarrantyUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    QrCodePayload = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReturnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    ProtocolNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "device_trust_tokens",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_trust_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_verification_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "external_logins",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderUserId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_logins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_profiles",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    DefaultManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_users",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    TotpSecret = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsTwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EmployeeNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    RelationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Location = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    CostCenter = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "procedures",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AppliesTo = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RequiresAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procedures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sent_alerts",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sent_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CurrentPeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostCenter = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "asset_field_definitions",
                schema: "tenebit",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Options = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_field_definitions", x => new { x.CategoryId, x.Id });
                    table.ForeignKey(
                        name: "FK_asset_field_definitions_asset_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "tenebit",
                        principalTable: "asset_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_field_values",
                schema: "tenebit",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asset_field_values", x => new { x.AssetId, x.FieldKey });
                    table.ForeignKey(
                        name: "FK_asset_field_values_assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "tenebit",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignment_assets",
                schema: "tenebit",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueCondition = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ReturnCondition = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_assets", x => new { x.AssignmentId, x.AssetId });
                    table.ForeignKey(
                        name: "FK_assignment_assets_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "tenebit",
                        principalTable: "assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "procedure_acceptances",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procedure_acceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_procedure_acceptances_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "tenebit",
                        principalTable: "assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_asset_categories",
                schema: "tenebit",
                columns: table => new
                {
                    JobProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_profile_asset_categories", x => new { x.JobProfileId, x.AssetCategoryId });
                    table.ForeignKey(
                        name: "FK_job_profile_asset_categories_job_profiles_JobProfileId",
                        column: x => x.JobProfileId,
                        principalSchema: "tenebit",
                        principalTable: "job_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_profile_procedures",
                schema: "tenebit",
                columns: table => new
                {
                    JobProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_profile_procedures", x => new { x.JobProfileId, x.ProcedureId });
                    table.ForeignKey(
                        name: "FK_job_profile_procedures_job_profiles_JobProfileId",
                        column: x => x.JobProfileId,
                        principalSchema: "tenebit",
                        principalTable: "job_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_user_roles",
                schema: "tenebit",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_user_roles", x => new { x.UserId, x.Role });
                    table.ForeignKey(
                        name: "FK_organization_user_roles_organization_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "tenebit",
                        principalTable: "organization_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "procedure_documents",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_procedure_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_procedure_documents_procedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalSchema: "tenebit",
                        principalTable: "procedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_logs_OrganizationId_CreatedAt",
                schema: "tenebit",
                table: "activity_logs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_categories_OrganizationId_Name",
                schema: "tenebit",
                table: "asset_categories",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_asset_status_settings_OrganizationId_StatusKey",
                schema: "tenebit",
                table: "asset_status_settings",
                columns: new[] { "OrganizationId", "StatusKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_OrganizationId_AssetTag",
                schema: "tenebit",
                table: "assets",
                columns: new[] { "OrganizationId", "AssetTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_OrganizationId_Status",
                schema: "tenebit",
                table: "assets",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_OrganizationId_ProtocolNumber",
                schema: "tenebit",
                table: "assignments",
                columns: new[] { "OrganizationId", "ProtocolNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_trust_tokens_OrganizationUserId_TokenHash",
                schema: "tenebit",
                table: "device_trust_tokens",
                columns: new[] { "OrganizationUserId", "TokenHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_OrganizationUserId",
                schema: "tenebit",
                table: "email_verification_tokens",
                column: "OrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_TokenHash",
                schema: "tenebit",
                table: "email_verification_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_logins_Provider_ProviderUserId",
                schema: "tenebit",
                table: "external_logins",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_profiles_OrganizationId_Name",
                schema: "tenebit",
                table: "job_profiles",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_users_Email",
                schema: "tenebit",
                table: "organization_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_users_OrganizationId_Email",
                schema: "tenebit",
                table: "organization_users",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_OrganizationUserId",
                schema: "tenebit",
                table: "password_reset_tokens",
                column: "OrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_TokenHash",
                schema: "tenebit",
                table: "password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_people_OrganizationId_Email",
                schema: "tenebit",
                table: "people",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procedure_acceptances_AssignmentId",
                schema: "tenebit",
                table: "procedure_acceptances",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_procedure_documents_OrganizationId_ProcedureId_UploadedAt",
                schema: "tenebit",
                table: "procedure_documents",
                columns: new[] { "OrganizationId", "ProcedureId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_procedure_documents_ProcedureId",
                schema: "tenebit",
                table: "procedure_documents",
                column: "ProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_procedures_OrganizationId_Title_Version",
                schema: "tenebit",
                table: "procedures",
                columns: new[] { "OrganizationId", "Title", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_OrganizationUserId",
                schema: "tenebit",
                table: "refresh_tokens",
                column: "OrganizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                schema: "tenebit",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sent_alerts_OrganizationId_AlertKey_EntityId",
                schema: "tenebit",
                table: "sent_alerts",
                columns: new[] { "OrganizationId", "AlertKey", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_OrganizationId",
                schema: "tenebit",
                table: "subscriptions",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_OrganizationId_Name",
                schema: "tenebit",
                table: "teams",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            // asset_locations has no EF entity mapping (accessed via raw SQL in LocationEndpoints);
            // kept here so the table still gets created now that TenebitSchemaPatch/EnsureRuntimeSchemaAsync are gone.
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS tenebit.asset_locations;");

            migrationBuilder.DropTable(
                name: "activity_logs",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "asset_field_definitions",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "asset_field_values",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "asset_status_settings",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "assignment_assets",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "device_trust_tokens",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "email_verification_tokens",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "external_logins",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "job_profile_asset_categories",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "job_profile_procedures",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "organization_user_roles",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "password_reset_tokens",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "people",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "procedure_acceptances",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "procedure_documents",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "sent_alerts",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "teams",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "asset_categories",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "job_profiles",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "organization_users",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "procedures",
                schema: "tenebit");
        }
    }
}

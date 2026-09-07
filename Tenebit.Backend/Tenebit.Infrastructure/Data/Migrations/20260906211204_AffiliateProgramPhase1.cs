using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AffiliateProgramPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "affiliate_country_discount_rules",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_country_discount_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_program_settings",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultCommissionPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CommissionBase = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DefaultCommissionWindowMonths = table.Column<int>(type: "integer", nullable: true),
                    DefaultMaxCodesPerAffiliate = table.Column<int>(type: "integer", nullable: false),
                    PayoutDayOfMonth = table.Column<int>(type: "integer", nullable: false),
                    PayoutGraceDays = table.Column<int>(type: "integer", nullable: false),
                    MinimumPayoutAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    CodeGrantsCustomerDiscountByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    PublicLeaderboardEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TermsVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_program_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "affiliates",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LastName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RevolutTag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommissionPercentOverride = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    MaxActiveCodesOverride = table.Column<int>(type: "integer", nullable: true),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedTermsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedTermsVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BlockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BlockedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_codes",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_codes_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_email_verification_tokens",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_email_verification_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_email_verification_tokens_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_message_threads",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UnreadByAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    UnreadByAffiliate = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_message_threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_message_threads_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_password_reset_tokens",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_password_reset_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_password_reset_tokens_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_payout_periods",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalCommission = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_payout_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_payout_periods_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_payouts",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CoveredPeriodIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    MarkedPaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_payouts_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_refresh_tokens",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_refresh_tokens_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_clicks",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IpHash = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: false),
                    UserAgentHash = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: true),
                    AttributionToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_clicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_clicks_affiliate_codes_AffiliateCodeId",
                        column: x => x.AffiliateCodeId,
                        principalSchema: "tenebit",
                        principalTable: "affiliate_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_conversions",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationSubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaddleTransactionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CommissionBase = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    IsWithinCommissionWindow = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AffiliatePayoutPeriodId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_conversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_conversions_affiliate_codes_AffiliateCodeId",
                        column: x => x.AffiliateCodeId,
                        principalSchema: "tenebit",
                        principalTable: "affiliate_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_affiliate_conversions_affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalSchema: "tenebit",
                        principalTable: "affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affiliate_messages",
                schema: "tenebit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affiliate_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_affiliate_messages_affiliate_message_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalSchema: "tenebit",
                        principalTable: "affiliate_message_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_clicks_AffiliateCodeId_IpHash_ClickedAt",
                schema: "tenebit",
                table: "affiliate_clicks",
                columns: new[] { "AffiliateCodeId", "IpHash", "ClickedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_codes_AffiliateId",
                schema: "tenebit",
                table: "affiliate_codes",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_codes_Code",
                schema: "tenebit",
                table: "affiliate_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_conversions_AffiliateCodeId",
                schema: "tenebit",
                table: "affiliate_conversions",
                column: "AffiliateCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_conversions_AffiliateId",
                schema: "tenebit",
                table: "affiliate_conversions",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_conversions_AffiliatePayoutPeriodId",
                schema: "tenebit",
                table: "affiliate_conversions",
                column: "AffiliatePayoutPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_conversions_PaddleTransactionId",
                schema: "tenebit",
                table: "affiliate_conversions",
                column: "PaddleTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_country_discount_rules_CountryCode",
                schema: "tenebit",
                table: "affiliate_country_discount_rules",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_email_verification_tokens_AffiliateId",
                schema: "tenebit",
                table: "affiliate_email_verification_tokens",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_email_verification_tokens_TokenHash",
                schema: "tenebit",
                table: "affiliate_email_verification_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_message_threads_AffiliateId",
                schema: "tenebit",
                table: "affiliate_message_threads",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_messages_ThreadId",
                schema: "tenebit",
                table: "affiliate_messages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_password_reset_tokens_AffiliateId",
                schema: "tenebit",
                table: "affiliate_password_reset_tokens",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_password_reset_tokens_TokenHash",
                schema: "tenebit",
                table: "affiliate_password_reset_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_payout_periods_AffiliateId_PeriodStart",
                schema: "tenebit",
                table: "affiliate_payout_periods",
                columns: new[] { "AffiliateId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_payouts_AffiliateId",
                schema: "tenebit",
                table: "affiliate_payouts",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_refresh_tokens_AffiliateId",
                schema: "tenebit",
                table: "affiliate_refresh_tokens",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_refresh_tokens_FamilyId",
                schema: "tenebit",
                table: "affiliate_refresh_tokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_affiliate_refresh_tokens_TokenHash",
                schema: "tenebit",
                table: "affiliate_refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_affiliates_Email",
                schema: "tenebit",
                table: "affiliates",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "affiliate_clicks",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_conversions",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_country_discount_rules",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_email_verification_tokens",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_messages",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_password_reset_tokens",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_payout_periods",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_payouts",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_program_settings",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_refresh_tokens",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_codes",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliate_message_threads",
                schema: "tenebit");

            migrationBuilder.DropTable(
                name: "affiliates",
                schema: "tenebit");
        }
    }
}

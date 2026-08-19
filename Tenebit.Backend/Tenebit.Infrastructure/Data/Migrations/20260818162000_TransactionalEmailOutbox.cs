using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenebit.Infrastructure.Data.Migrations;

[DbContext(typeof(TenebitDbContext))]
[Migration("20260818162000_TransactionalEmailOutbox")]
public partial class TransactionalEmailOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "email_outbox_messages",
            schema: "tenebit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientCiphertext = table.Column<string>(type: "text", nullable: false),
                SubjectCiphertext = table.Column<string>(type: "text", nullable: false),
                HtmlCiphertext = table.Column<string>(type: "text", nullable: false),
                Purpose = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_email_outbox_messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_email_outbox_messages_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "tenebit",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_email_outbox_messages_AttemptCount",
            schema: "tenebit",
            table: "email_outbox_messages",
            sql: "\"AttemptCount\" >= 0");

        migrationBuilder.CreateIndex(
            name: "IX_email_outbox_messages_OrganizationId_IdempotencyKey",
            schema: "tenebit",
            table: "email_outbox_messages",
            columns: new[] { "OrganizationId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_email_outbox_messages_dispatch",
            schema: "tenebit",
            table: "email_outbox_messages",
            columns: new[] { "SentAt", "NextAttemptAt", "LeaseUntil", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "email_outbox_messages", schema: "tenebit");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredSystemAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "SystemAuditLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "other");

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityId",
                table: "SystemAuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "SystemAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "SystemAuditLogs"
                SET "Category" = CASE
                    WHEN lower("Action") LIKE 'task.%'
                        OR lower("EntityType") = 'processtask'
                        THEN 'tasks'
                    WHEN lower("Action") LIKE 'process.%'
                        OR lower("Action") LIKE 'processdefinition.%'
                        OR lower("Action") LIKE 'workflow.%'
                        OR lower("EntityType") IN ('processinstance', 'processdefinition', 'processdefinitionversion')
                        THEN 'processes'
                    WHEN lower("Action") LIKE 'formdefinition.%'
                        OR lower("Action") LIKE 'formversion.%'
                        OR lower("EntityType") IN ('formdefinition', 'formdefinitionversion')
                        THEN 'forms'
                    WHEN lower("Action") LIKE 'community.%'
                        OR lower("Action") LIKE 'communityrole.%'
                        OR lower("Action") LIKE 'team.%'
                        OR lower("Action") LIKE 'user.access%'
                        OR lower("Action") LIKE 'user.pendingapproval%'
                        OR lower("Action") IN ('user.createdbyadmin', 'user.deletedbyadmin')
                        THEN 'access'
                    WHEN lower("Action") LIKE 'auth.%'
                        OR lower("Action") LIKE 'user.profile%'
                        OR lower("Action") LIKE 'user.password%'
                        THEN 'identity'
                    ELSE 'other'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SystemAuditLogs_CommunityId_Category_CreatedAt",
                table: "SystemAuditLogs",
                columns: new[] { "CommunityId", "Category", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SystemAuditLogs_Communities_CommunityId",
                table: "SystemAuditLogs",
                column: "CommunityId",
                principalTable: "Communities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SystemAuditLogs_Communities_CommunityId",
                table: "SystemAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemAuditLogs_CommunityId_Category_CreatedAt",
                table: "SystemAuditLogs");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "SystemAuditLogs");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "SystemAuditLogs");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "SystemAuditLogs");
        }
    }
}

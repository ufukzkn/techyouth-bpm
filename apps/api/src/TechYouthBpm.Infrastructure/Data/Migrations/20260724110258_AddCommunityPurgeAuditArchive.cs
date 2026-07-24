using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityPurgeAuditArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CommunityId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityDeletionArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalCommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityName = table.Column<string>(type: "text", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedByUsername = table.Column<string>(type: "text", nullable: false),
                    DeletedByDisplayName = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserCount = table.Column<int>(type: "integer", nullable: false),
                    PreservedUserCount = table.Column<int>(type: "integer", nullable: false),
                    CommunityRoleCount = table.Column<int>(type: "integer", nullable: false),
                    TeamCount = table.Column<int>(type: "integer", nullable: false),
                    FormCount = table.Column<int>(type: "integer", nullable: false),
                    WorkflowCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessCount = table.Column<int>(type: "integer", nullable: false),
                    TaskCount = table.Column<int>(type: "integer", nullable: false),
                    NotificationCount = table.Column<int>(type: "integer", nullable: false),
                    SystemAuditCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessStepCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDeletionArchives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityDeletionArchiveId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorDisplayName = table.Column<string>(type: "text", nullable: false),
                    ActorUsername = table.Column<string>(type: "text", nullable: false),
                    EntityDisplayName = table.Column<string>(type: "text", nullable: true),
                    EntityUsername = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    NodeTitle = table.Column<string>(type: "text", nullable: false),
                    TeamName = table.Column<string>(type: "text", nullable: false),
                    CommunityRoleName = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedAuditEvents_CommunityDeletionArchives_CommunityDele~",
                        column: x => x.CommunityDeletionArchiveId,
                        principalTable: "CommunityDeletionArchives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CommunityId_CreatedAt",
                table: "Notifications",
                columns: new[] { "CommunityId", "CreatedAt" });

            migrationBuilder.Sql(
                """
                UPDATE "Notifications"
                SET "CommunityId" = (
                    SELECT "UserCommunityMemberships"."CommunityId"
                    FROM "UserCommunityMemberships"
                    WHERE "UserCommunityMemberships"."UserId" = "Notifications"."UserId"
                      AND "UserCommunityMemberships"."IsActive" = TRUE
                    ORDER BY "UserCommunityMemberships"."CreatedAt" DESC
                    LIMIT 1
                )
                WHERE "CommunityId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedAuditEvents_CommunityDeletionArchiveId_Category_Occ~",
                table: "ArchivedAuditEvents",
                columns: new[] { "CommunityDeletionArchiveId", "Category", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDeletionArchives_DeletedAt",
                table: "CommunityDeletionArchives",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDeletionArchives_OriginalCommunityId",
                table: "CommunityDeletionArchives",
                column: "OriginalCommunityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Communities_CommunityId",
                table: "Notifications",
                column: "CommunityId",
                principalTable: "Communities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Communities_CommunityId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "ArchivedAuditEvents");

            migrationBuilder.DropTable(
                name: "CommunityDeletionArchives");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CommunityId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "Notifications");
        }
    }
}

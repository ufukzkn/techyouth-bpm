using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260722061500_OptimizeWorkflowListIndexes")]
public partial class OptimizeWorkflowListIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_ProcessInstances_CommunityId", table: "ProcessInstances");
        migrationBuilder.DropIndex(name: "IX_ProcessInstances_StartedByUserId", table: "ProcessInstances");
        migrationBuilder.DropIndex(name: "IX_ProcessTasks_CandidateCommunityRoleId", table: "ProcessTasks");
        migrationBuilder.DropIndex(name: "IX_ProcessTasks_CandidateTeamId", table: "ProcessTasks");

        migrationBuilder.CreateIndex(
            name: "IX_ProcessInstances_CommunityId_Status_StartedAt",
            table: "ProcessInstances",
            columns: new[] { "CommunityId", "Status", "StartedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcessInstances_StartedByUserId_Status_StartedAt",
            table: "ProcessInstances",
            columns: new[] { "StartedByUserId", "Status", "StartedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcessTasks_CandidateCommunityRoleId_Status_ClaimedByUserId",
            table: "ProcessTasks",
            columns: new[] { "CandidateCommunityRoleId", "Status", "ClaimedByUserId" });
        migrationBuilder.CreateIndex(
            name: "IX_ProcessTasks_CandidateTeamId_Status_ClaimedByUserId",
            table: "ProcessTasks",
            columns: new[] { "CandidateTeamId", "Status", "ClaimedByUserId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_ProcessInstances_CommunityId_Status_StartedAt", table: "ProcessInstances");
        migrationBuilder.DropIndex(name: "IX_ProcessInstances_StartedByUserId_Status_StartedAt", table: "ProcessInstances");
        migrationBuilder.DropIndex(name: "IX_ProcessTasks_CandidateCommunityRoleId_Status_ClaimedByUserId", table: "ProcessTasks");
        migrationBuilder.DropIndex(name: "IX_ProcessTasks_CandidateTeamId_Status_ClaimedByUserId", table: "ProcessTasks");

        migrationBuilder.CreateIndex(name: "IX_ProcessInstances_CommunityId", table: "ProcessInstances", column: "CommunityId");
        migrationBuilder.CreateIndex(name: "IX_ProcessInstances_StartedByUserId", table: "ProcessInstances", column: "StartedByUserId");
        migrationBuilder.CreateIndex(name: "IX_ProcessTasks_CandidateCommunityRoleId", table: "ProcessTasks", column: "CandidateCommunityRoleId");
        migrationBuilder.CreateIndex(name: "IX_ProcessTasks_CandidateTeamId", table: "ProcessTasks", column: "CandidateTeamId");
    }
}

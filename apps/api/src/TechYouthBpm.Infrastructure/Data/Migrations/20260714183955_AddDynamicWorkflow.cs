using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedUserId",
                table: "ProcessTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentType",
                table: "ProcessTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "ProcessTasks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "CandidateCommunityRoleId",
                table: "ProcessTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CandidateTeamId",
                table: "ProcessTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimVersion",
                table: "ProcessTasks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "ProcessTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimedByUserId",
                table: "ProcessTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FormDefinitionVersionId",
                table: "ProcessTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NodeKey",
                table: "ProcessTasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ProcessTasks",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ProcessTasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CurrentNodeKey",
                table: "ProcessInstances",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FormDefinitionVersionId",
                table: "ProcessInstances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessDefinitionVersionId",
                table: "ProcessInstances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariablesJson",
                table: "ProcessInstances",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "FormDefinitionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormDefinitionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormDefinitionVersions_FormDefinitions_FormDefinitionId",
                        column: x => x.FormDefinitionId,
                        principalTable: "FormDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FormDefinitionVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormDefinitionVersions_Users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProcessDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitions_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProcessStepExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeKey = table.Column<string>(type: "text", nullable: false),
                    NodeType = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: true),
                    OutputJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessStepExecutions_ProcessInstances_ProcessInstanceId",
                        column: x => x.ProcessInstanceId,
                        principalTable: "ProcessInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessStepExecutions_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FormPageDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormPageDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormPageDefinitions_FormDefinitionVersions_FormDefinitionVe~",
                        column: x => x.FormDefinitionVersionId,
                        principalTable: "FormDefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessDefinitionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FormDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GraphJson = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessDefinitionVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitionVersions_FormDefinitionVersions_FormDefini~",
                        column: x => x.FormDefinitionVersionId,
                        principalTable: "FormDefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitionVersions_ProcessDefinitions_ProcessDefinit~",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "ProcessDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitionVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessDefinitionVersions_Users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FormVersionFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormPageDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormVersionFieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormVersionFieldDefinitions_FormPageDefinitions_FormPageDef~",
                        column: x => x.FormPageDefinitionId,
                        principalTable: "FormPageDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormVersionFieldValidationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormVersionFieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    DependsOnFieldKey = table.Column<string>(type: "text", nullable: false),
                    ExpectedValue = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormVersionFieldValidationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormVersionFieldValidationRules_FormVersionFieldDefinitions~",
                        column: x => x.FormVersionFieldDefinitionId,
                        principalTable: "FormVersionFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_AssignedUserId_Status",
                table: "ProcessTasks",
                columns: new[] { "AssignedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_CandidateCommunityRoleId",
                table: "ProcessTasks",
                column: "CandidateCommunityRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_CandidateTeamId",
                table: "ProcessTasks",
                column: "CandidateTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_ClaimedByUserId_Status",
                table: "ProcessTasks",
                columns: new[] { "ClaimedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_FormDefinitionVersionId",
                table: "ProcessTasks",
                column: "FormDefinitionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_Status_Priority_CreatedAt",
                table: "ProcessTasks",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_FormDefinitionVersionId",
                table: "ProcessInstances",
                column: "FormDefinitionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_ProcessDefinitionVersionId",
                table: "ProcessInstances",
                column: "ProcessDefinitionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitionVersions_CreatedByUserId",
                table: "FormDefinitionVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitionVersions_FormDefinitionId_Status",
                table: "FormDefinitionVersions",
                columns: new[] { "FormDefinitionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitionVersions_FormDefinitionId_VersionNumber",
                table: "FormDefinitionVersions",
                columns: new[] { "FormDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitionVersions_PublishedByUserId",
                table: "FormDefinitionVersions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormPageDefinitions_FormDefinitionVersionId_Key",
                table: "FormPageDefinitions",
                columns: new[] { "FormDefinitionVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormPageDefinitions_FormDefinitionVersionId_SortOrder",
                table: "FormPageDefinitions",
                columns: new[] { "FormDefinitionVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FormVersionFieldDefinitions_FormPageDefinitionId_Key",
                table: "FormVersionFieldDefinitions",
                columns: new[] { "FormPageDefinitionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormVersionFieldValidationRules_FormVersionFieldDefinitionId",
                table: "FormVersionFieldValidationRules",
                column: "FormVersionFieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_CommunityId_Name",
                table: "ProcessDefinitions",
                columns: new[] { "CommunityId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_CreatedByUserId",
                table: "ProcessDefinitions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitions_UpdatedByUserId",
                table: "ProcessDefinitions",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitionVersions_CreatedByUserId",
                table: "ProcessDefinitionVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitionVersions_FormDefinitionVersionId",
                table: "ProcessDefinitionVersions",
                column: "FormDefinitionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitionVersions_ProcessDefinitionId_Status",
                table: "ProcessDefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitionVersions_ProcessDefinitionId_VersionNumber",
                table: "ProcessDefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessDefinitionVersions_PublishedByUserId",
                table: "ProcessDefinitionVersions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepExecutions_CompletedByUserId",
                table: "ProcessStepExecutions",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepExecutions_ProcessInstanceId_NodeKey_Attempt",
                table: "ProcessStepExecutions",
                columns: new[] { "ProcessInstanceId", "NodeKey", "Attempt" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessInstances_FormDefinitionVersions_FormDefinitionVersi~",
                table: "ProcessInstances",
                column: "FormDefinitionVersionId",
                principalTable: "FormDefinitionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessInstances_ProcessDefinitionVersions_ProcessDefinitio~",
                table: "ProcessInstances",
                column: "ProcessDefinitionVersionId",
                principalTable: "ProcessDefinitionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_CommunityRoles_CandidateCommunityRoleId",
                table: "ProcessTasks",
                column: "CandidateCommunityRoleId",
                principalTable: "CommunityRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_FormDefinitionVersions_FormDefinitionVersionId",
                table: "ProcessTasks",
                column: "FormDefinitionVersionId",
                principalTable: "FormDefinitionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_Teams_CandidateTeamId",
                table: "ProcessTasks",
                column: "CandidateTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_Users_AssignedUserId",
                table: "ProcessTasks",
                column: "AssignedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_Users_ClaimedByUserId",
                table: "ProcessTasks",
                column: "ClaimedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_FormDefinitionVersions_FormDefinitionVersi~",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_ProcessDefinitionVersions_ProcessDefinitio~",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_CommunityRoles_CandidateCommunityRoleId",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_FormDefinitionVersions_FormDefinitionVersionId",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_Teams_CandidateTeamId",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_Users_AssignedUserId",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_Users_ClaimedByUserId",
                table: "ProcessTasks");

            migrationBuilder.DropTable(
                name: "FormVersionFieldValidationRules");

            migrationBuilder.DropTable(
                name: "ProcessDefinitionVersions");

            migrationBuilder.DropTable(
                name: "ProcessStepExecutions");

            migrationBuilder.DropTable(
                name: "FormVersionFieldDefinitions");

            migrationBuilder.DropTable(
                name: "ProcessDefinitions");

            migrationBuilder.DropTable(
                name: "FormPageDefinitions");

            migrationBuilder.DropTable(
                name: "FormDefinitionVersions");

            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_AssignedUserId_Status",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_CandidateCommunityRoleId",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_CandidateTeamId",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_ClaimedByUserId_Status",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_FormDefinitionVersionId",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_Status_Priority_CreatedAt",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessInstances_FormDefinitionVersionId",
                table: "ProcessInstances");

            migrationBuilder.DropIndex(
                name: "IX_ProcessInstances_ProcessDefinitionVersionId",
                table: "ProcessInstances");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "CandidateCommunityRoleId",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "CandidateTeamId",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "ClaimVersion",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "ClaimedByUserId",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "FormDefinitionVersionId",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "NodeKey",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "CurrentNodeKey",
                table: "ProcessInstances");

            migrationBuilder.DropColumn(
                name: "FormDefinitionVersionId",
                table: "ProcessInstances");

            migrationBuilder.DropColumn(
                name: "ProcessDefinitionVersionId",
                table: "ProcessInstances");

            migrationBuilder.DropColumn(
                name: "VariablesJson",
                table: "ProcessInstances");
        }
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260720143000_AddTaskHistoryAndStepContext")]
public partial class AddTaskHistoryAndStepContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AssignmentType",
            table: "ProcessStepExecutions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AssignedUserNameSnapshot",
            table: "ProcessStepExecutions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "CommunityRoleNameSnapshot",
            table: "ProcessStepExecutions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "NodeTitle",
            table: "ProcessStepExecutions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Note",
            table: "ProcessStepExecutions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "TeamNameSnapshot",
            table: "ProcessStepExecutions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "CompletedAction",
            table: "ProcessTasks",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CompletionNote",
            table: "ProcessTasks",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AssignmentType", table: "ProcessStepExecutions");
        migrationBuilder.DropColumn(name: "AssignedUserNameSnapshot", table: "ProcessStepExecutions");
        migrationBuilder.DropColumn(name: "CommunityRoleNameSnapshot", table: "ProcessStepExecutions");
        migrationBuilder.DropColumn(name: "NodeTitle", table: "ProcessStepExecutions");
        migrationBuilder.DropColumn(name: "Note", table: "ProcessStepExecutions");
        migrationBuilder.DropColumn(name: "TeamNameSnapshot", table: "ProcessStepExecutions");
        migrationBuilder.DropColumn(name: "CompletedAction", table: "ProcessTasks");
        migrationBuilder.DropColumn(name: "CompletionNote", table: "ProcessTasks");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_Status_Priority_CreatedAt",
                table: "ProcessTasks");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "ProcessTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_Status_DueAt_Priority_CreatedAt",
                table: "ProcessTasks",
                columns: new[] { "Status", "DueAt", "Priority", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_Status_DueAt_Priority_CreatedAt",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "ProcessTasks");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_Status_Priority_CreatedAt",
                table: "ProcessTasks",
                columns: new[] { "Status", "Priority", "CreatedAt" });
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunitiesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedCommunityRoleId",
                table: "ProcessTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredPermission",
                table: "ProcessTasks",
                type: "text",
                nullable: false,
                defaultValue: "Tasks.Act");

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityId",
                table: "ProcessInstances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("10101010-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "CommunityId",
                table: "FormDefinitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("10101010-0000-0000-0000-000000000001"));

            migrationBuilder.CreateTable(
                name: "Communities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Communities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunityRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TemplateKey = table.Column<string>(type: "text", nullable: false),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityRoles_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityRolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permission = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityRolePermissions_CommunityRoles_CommunityRoleId",
                        column: x => x.CommunityRoleId,
                        principalTable: "CommunityRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCommunityMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCommunityMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCommunityMemberships_Communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "Communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCommunityMemberships_CommunityRoles_CommunityRoleId",
                        column: x => x.CommunityRoleId,
                        principalTable: "CommunityRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCommunityMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Communities",
                columns: new[] { "Id", "Name", "Description", "IsActive", "CreatedAt" },
                values: new object[,]
                {
                    { new Guid("10101010-0000-0000-0000-000000000001"), "Sportif Faaliyetler", "Transfer, teknik ekip onayi ve sportif operasyon surecleri.", true, DateTime.UtcNow },
                    { new Guid("10101010-0000-0000-0000-000000000002"), "Lojistik", "Kargo, sevkiyat ve teslimat operasyon surecleri.", true, DateTime.UtcNow },
                    { new Guid("10101010-0000-0000-0000-000000000003"), "Urun Siparisi", "Siparis talebi, stok kontrolu ve onay surecleri.", true, DateTime.UtcNow }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_AssignedCommunityRoleId",
                table: "ProcessTasks",
                column: "AssignedCommunityRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessInstances_CommunityId",
                table: "ProcessInstances",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_FormDefinitions_CommunityId",
                table: "FormDefinitions",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Communities_Name",
                table: "Communities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRolePermissions_CommunityRoleId_Permission",
                table: "CommunityRolePermissions",
                columns: new[] { "CommunityRoleId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRoles_CommunityId_Name",
                table: "CommunityRoles",
                columns: new[] { "CommunityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCommunityMemberships_CommunityId",
                table: "UserCommunityMemberships",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCommunityMemberships_CommunityRoleId",
                table: "UserCommunityMemberships",
                column: "CommunityRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCommunityMemberships_UserId_IsActive",
                table: "UserCommunityMemberships",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_FormDefinitions_Communities_CommunityId",
                table: "FormDefinitions",
                column: "CommunityId",
                principalTable: "Communities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessInstances_Communities_CommunityId",
                table: "ProcessInstances",
                column: "CommunityId",
                principalTable: "Communities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_CommunityRoles_AssignedCommunityRoleId",
                table: "ProcessTasks",
                column: "AssignedCommunityRoleId",
                principalTable: "CommunityRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormDefinitions_Communities_CommunityId",
                table: "FormDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessInstances_Communities_CommunityId",
                table: "ProcessInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_CommunityRoles_AssignedCommunityRoleId",
                table: "ProcessTasks");

            migrationBuilder.DropTable(
                name: "CommunityRolePermissions");

            migrationBuilder.DropTable(
                name: "UserCommunityMemberships");

            migrationBuilder.DropTable(
                name: "CommunityRoles");

            migrationBuilder.DropTable(
                name: "Communities");

            migrationBuilder.DropIndex(
                name: "IX_ProcessTasks_AssignedCommunityRoleId",
                table: "ProcessTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessInstances_CommunityId",
                table: "ProcessInstances");

            migrationBuilder.DropIndex(
                name: "IX_FormDefinitions_CommunityId",
                table: "FormDefinitions");

            migrationBuilder.DropColumn(
                name: "AssignedCommunityRoleId",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "RequiredPermission",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "ProcessInstances");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                table: "FormDefinitions");
        }
    }
}

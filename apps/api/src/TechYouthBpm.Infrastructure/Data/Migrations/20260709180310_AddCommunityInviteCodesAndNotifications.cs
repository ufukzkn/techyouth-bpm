using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechYouthBpm.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityInviteCodesAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "Communities",
                type: "text",
                nullable: false,
                defaultValue: "");

            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql("""
                    UPDATE "Communities"
                    SET "InviteCode" = data."InviteCode"
                    FROM (
                        SELECT "Id", 'C' || LPAD(ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Name")::text, 4, '0') AS "InviteCode"
                        FROM "Communities"
                    ) AS data
                    WHERE "Communities"."Id" = data."Id" AND "Communities"."InviteCode" = '';
                    """);
            }
            else
            {
                migrationBuilder.Sql("""
                    UPDATE "Communities"
                    SET "InviteCode" = printf('C%04d', rowid)
                    WHERE "InviteCode" = '';
                    """);
            }

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Communities_InviteCode",
                table: "Communities",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Communities_InviteCode",
                table: "Communities");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "Communities");
        }
    }
}

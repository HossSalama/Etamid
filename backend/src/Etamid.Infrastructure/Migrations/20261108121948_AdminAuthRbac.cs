using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Etamid.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminAuthRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReplacedByTokenId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminRefreshTokens_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminTwoFactorChallenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTwoFactorChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminTwoFactorChallenges_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AdminPermissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { 1L, "admin.users.view", "admin.users.view" },
                    { 2L, "admin.users.manage", "admin.users.manage" },
                    { 3L, "admin.users.nafath-review", "admin.users.nafath-review" },
                    { 4L, "admin.deals.view", "admin.deals.view" },
                    { 5L, "admin.deals.manage", "admin.deals.manage" },
                    { 6L, "admin.disputes.view", "admin.disputes.view" },
                    { 7L, "admin.disputes.decide", "admin.disputes.decide" },
                    { 8L, "admin.withdrawals.view", "admin.withdrawals.view" },
                    { 9L, "admin.withdrawals.approve", "admin.withdrawals.approve" },
                    { 10L, "admin.withdrawals.complete", "admin.withdrawals.complete" },
                    { 11L, "admin.penalties.view", "admin.penalties.view" },
                    { 12L, "admin.penalties.waive", "admin.penalties.waive" },
                    { 13L, "admin.content.manage", "admin.content.manage" },
                    { 14L, "admin.settings.manage", "admin.settings.manage" },
                    { 15L, "admin.notifications.send", "admin.notifications.send" },
                    { 16L, "admin.support.manage", "admin.support.manage" },
                    { 17L, "admin.roles.manage", "admin.roles.manage" },
                    { 18L, "admin.audit.view", "admin.audit.view" }
                });

            migrationBuilder.InsertData(
                table: "AdminRoles",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 1L, "Holds every admin permission. Protected: cannot be edited or deleted.", "super-admin" });

            migrationBuilder.InsertData(
                table: "AdminRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1L, 1L },
                    { 2L, 1L },
                    { 3L, 1L },
                    { 4L, 1L },
                    { 5L, 1L },
                    { 6L, 1L },
                    { 7L, 1L },
                    { 8L, 1L },
                    { 9L, 1L },
                    { 10L, 1L },
                    { 11L, 1L },
                    { 12L, 1L },
                    { 13L, 1L },
                    { 14L, 1L },
                    { 15L, 1L },
                    { 16L, 1L },
                    { 17L, 1L },
                    { 18L, 1L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminRefreshTokens_AdminUserId",
                table: "AdminRefreshTokens",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminRefreshTokens_FamilyId",
                table: "AdminRefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminRefreshTokens_TokenHash",
                table: "AdminRefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminTwoFactorChallenges_AdminUserId",
                table: "AdminTwoFactorChallenges",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminTwoFactorChallenges_TokenHash",
                table: "AdminTwoFactorChallenges",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminRefreshTokens");

            migrationBuilder.DropTable(
                name: "AdminTwoFactorChallenges");

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 1L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 2L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 3L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 4L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 5L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 6L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 7L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 8L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 9L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 10L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 11L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 12L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 13L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 14L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 15L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 16L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 17L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 18L, 1L });

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 9L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 10L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 11L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 12L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 13L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 14L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 15L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 16L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 17L);

            migrationBuilder.DeleteData(
                table: "AdminPermissions",
                keyColumn: "Id",
                keyValue: 18L);

            migrationBuilder.DeleteData(
                table: "AdminRoles",
                keyColumn: "Id",
                keyValue: 1L);
        }
    }
}

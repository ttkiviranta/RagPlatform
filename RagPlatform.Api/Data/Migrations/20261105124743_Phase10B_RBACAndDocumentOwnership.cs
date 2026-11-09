using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RagPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase10B_RBACAndDocumentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EntraObjectId",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // Legacy rows belong to this single-tenant app; ownership is intentionally left unknown.
            migrationBuilder.Sql("""
                UPDATE "Users"
                SET "TenantId" = '6b50c2f8-c252-4fc8-b634-91801ee9cc64'
                WHERE "TenantId" = '';

                UPDATE "Documents"
                SET "TenantId" = '6b50c2f8-c252-4fc8-b634-91801ee9cc64'
                WHERE "TenantId" IS NULL OR "TenantId" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "Documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleName });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleName",
                        column: x => x.RoleName,
                        principalTable: "Roles",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Name", "Description" },
                values: new object[,]
                {
                    { "PowerUser", "Tenant-wide document management." },
                    { "SuperUser", "Tenant-wide document and role administration." },
                    { "User", "Access to owned documents." }
                });

            migrationBuilder.Sql("""
                INSERT INTO "UserRoles" ("UserId", "RoleName", "AssignedAt")
                SELECT "Id", 'User', CURRENT_TIMESTAMP
                FROM "Users"
                ON CONFLICT ("UserId", "RoleName") DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_EntraObjectId",
                table: "Users",
                columns: new[] { "TenantId", "EntraObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerUserId",
                table: "Documents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TenantId_OwnerUserId_CreatedAt",
                table: "Documents",
                columns: new[] { "TenantId", "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleName",
                table: "UserRoles",
                column: "RoleName");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_OwnerUserId",
                table: "Documents",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_OwnerUserId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_EntraObjectId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Documents_OwnerUserId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_TenantId_OwnerUserId_CreatedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraObjectId",
                table: "Users",
                column: "EntraObjectId",
                unique: true);
        }
    }
}

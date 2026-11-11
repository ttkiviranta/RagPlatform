using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase11FoundryInvocationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InputTokenCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelDeployment",
                table: "AuditEvents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelOperation",
                table: "AuditEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelProvider",
                table: "AuditEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokenCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputTokenCount",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ModelDeployment",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ModelOperation",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ModelProvider",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "OutputTokenCount",
                table: "AuditEvents");
        }
    }
}

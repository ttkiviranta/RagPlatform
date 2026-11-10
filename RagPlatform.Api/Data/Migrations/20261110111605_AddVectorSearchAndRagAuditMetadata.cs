using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorSearchAndRagAuditMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageSimilarity",
                table: "AuditEvents",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContextCharacterCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationMilliseconds",
                table: "AuditEvents",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputCharacterCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromptCharacterCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedTopK",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageSimilarity",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ContextCharacterCount",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "DurationMilliseconds",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "InputCharacterCount",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "PromptCharacterCount",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "RequestedTopK",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ResultCount",
                table: "AuditEvents");
        }
    }
}

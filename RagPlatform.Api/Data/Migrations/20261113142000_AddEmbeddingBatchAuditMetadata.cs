using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RagPlatform.Api.Data;

#nullable disable

namespace RagPlatform.Api.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261113142000_AddEmbeddingBatchAuditMetadata")]
    public partial class AddEmbeddingBatchAuditMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeneratedEmbeddingCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReusedEmbeddingCount",
                table: "AuditEvents",
                type: "integer",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedEmbeddingCount",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "ReusedEmbeddingCount",
                table: "AuditEvents");
        }
    }
}

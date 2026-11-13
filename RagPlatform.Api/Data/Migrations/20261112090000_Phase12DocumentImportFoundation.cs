using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RagPlatform.Api.Data;

#nullable disable

namespace RagPlatform.Api.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261112090000_Phase12DocumentImportFoundation")]
    public partial class Phase12DocumentImportFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Documents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.Sql("UPDATE \"Documents\" SET \"UpdatedAt\" = \"CreatedAt\"");

            migrationBuilder.AddColumn<string>(
                name: "ImportStatus",
                table: "Documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StagingBlobKey",
                table: "Documents",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "StagingBlobKey",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Documents");
        }
    }
}

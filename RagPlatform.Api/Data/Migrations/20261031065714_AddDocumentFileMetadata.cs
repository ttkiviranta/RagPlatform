using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "Documents");
        }
    }
}

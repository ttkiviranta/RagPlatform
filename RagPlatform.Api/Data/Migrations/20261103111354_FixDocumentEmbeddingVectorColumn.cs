using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace RagPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixDocumentEmbeddingVectorColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "EmbeddingVector",
                table: "DocumentEmbeddings",
                type: "vector",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"DocumentEmbeddings\" SET \"EmbeddingVector\" = '[0]' WHERE \"EmbeddingVector\" IS NULL;");

            migrationBuilder.AlterColumn<Vector>(
                name: "EmbeddingVector",
                table: "DocumentEmbeddings",
                type: "vector",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingVector",
                table: "DocumentEmbeddings");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}

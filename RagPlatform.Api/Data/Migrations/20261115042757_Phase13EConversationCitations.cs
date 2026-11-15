using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RagPlatform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase13EConversationCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationCitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitationNumber = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    Excerpt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationCitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationCitations_ConversationMessages_ConversationMess~",
                        column: x => x.ConversationMessageId,
                        principalTable: "ConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationCitations_ConversationMessageId_CitationNumber",
                table: "ConversationCitations",
                columns: new[] { "ConversationMessageId", "CitationNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationCitations");
        }
    }
}

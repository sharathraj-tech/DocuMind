using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixCitationForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_citations_document_chunks_chunk_id",
                table: "citations");

            migrationBuilder.DropForeignKey(
                name: "fk_citations_messages_chunk_id",
                table: "citations");

            migrationBuilder.CreateIndex(
                name: "ix_citations_message_id",
                table: "citations",
                column: "message_id");

            migrationBuilder.AddForeignKey(
                name: "fk_citations_document_chunks_chunk_id",
                table: "citations",
                column: "chunk_id",
                principalTable: "document_chunks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_citations_messages_message_id",
                table: "citations",
                column: "message_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_citations_document_chunks_chunk_id",
                table: "citations");

            migrationBuilder.DropForeignKey(
                name: "fk_citations_messages_message_id",
                table: "citations");

            migrationBuilder.DropIndex(
                name: "ix_citations_message_id",
                table: "citations");

            migrationBuilder.AddForeignKey(
                name: "fk_citations_document_chunks_chunk_id",
                table: "citations",
                column: "chunk_id",
                principalTable: "document_chunks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_citations_messages_chunk_id",
                table: "citations",
                column: "chunk_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

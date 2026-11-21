using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentUploadSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_tenant_id_content_hash",
                table: "documents");

            migrationBuilder.AlterColumn<string>(
                name: "error",
                table: "documents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_tenant_id_content_hash",
                table: "documents",
                columns: new[] { "tenant_id", "content_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_tenant_id_content_hash",
                table: "documents");

            migrationBuilder.AlterColumn<string>(
                name: "error",
                table: "documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_tenant_id_content_hash",
                table: "documents",
                columns: new[] { "tenant_id", "content_hash" });
        }
    }
}

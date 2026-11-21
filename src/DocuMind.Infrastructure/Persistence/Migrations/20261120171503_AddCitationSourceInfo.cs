using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCitationSourceInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "document_id",
                table: "citations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_index",
                table: "citations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_id",
                table: "citations");

            migrationBuilder.DropColumn(
                name: "source_index",
                table: "citations");
        }
    }
}

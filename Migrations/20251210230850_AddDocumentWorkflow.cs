using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JyotiIyerCPA.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientResponseText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientResponseDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentWorkflows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentWorkflows_Documents_ClientResponseDocumentId",
                        column: x => x.ClientResponseDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentWorkflows_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentWorkflows_AdminUserId_Status",
                table: "DocumentWorkflows",
                columns: new[] { "AdminUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentWorkflows_ClientResponseDocumentId",
                table: "DocumentWorkflows",
                column: "ClientResponseDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentWorkflows_ClientUserId_Status",
                table: "DocumentWorkflows",
                columns: new[] { "ClientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentWorkflows_DocumentId",
                table: "DocumentWorkflows",
                column: "DocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentWorkflows");
        }
    }
}

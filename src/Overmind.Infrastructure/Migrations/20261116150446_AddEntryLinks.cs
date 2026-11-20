using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ToId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Origin = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryLinks_ContextEntries_FromEntryId",
                        column: x => x.FromEntryId,
                        principalTable: "ContextEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryLinks_FromEntryId_ToKind_ToId",
                table: "EntryLinks",
                columns: new[] { "FromEntryId", "ToKind", "ToId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryLinks_ToKind_ToId",
                table: "EntryLinks",
                columns: new[] { "ToKind", "ToId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryLinks");
        }
    }
}

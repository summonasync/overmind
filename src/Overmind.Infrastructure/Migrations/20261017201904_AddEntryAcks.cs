using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddEntryAcks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EntryAcks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ContextEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                Alias = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                Vendor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                At = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EntryAcks", x => x.Id);
                table.ForeignKey(
                    name: "FK_EntryAcks_ContextEntries_ContextEntryId",
                    column: x => x.ContextEntryId,
                    principalTable: "ContextEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EntryAcks_ContextEntryId",
            table: "EntryAcks",
            column: "ContextEntryId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE "EntryAcks";
            """);
    }
}

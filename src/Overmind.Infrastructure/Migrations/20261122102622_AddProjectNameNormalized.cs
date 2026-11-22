using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectNameNormalized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // SQLite lower() folds ASCII letters only: non-Latin names keep their original
            // casing in NameNormalized until the next C# write re-normalizes them. The live
            // store is Latin-only (probe 2026-11-21: 18 projects, zero case-insensitive
            // duplicate groups), so this backfill covers every row today.
            migrationBuilder.Sql("UPDATE Projects SET NameNormalized = lower(Name)");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_NameNormalized",
                table: "Projects",
                column: "NameNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_NameNormalized",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "Projects");
        }
    }
}

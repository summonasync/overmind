using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueKitName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH duplicates AS MATERIALIZED (
                    SELECT
                        rowid AS DuplicateRowid,
                        Name || ' (' || (
                            SELECT COUNT(*) + 1
                            FROM SkillKits AS earlier
                            WHERE earlier.Name = SkillKits.Name
                              AND earlier.rowid < SkillKits.rowid
                        ) || ')' AS DeduplicatedName
                    FROM SkillKits
                    WHERE rowid NOT IN (SELECT MIN(rowid) FROM SkillKits GROUP BY Name)
                )
                UPDATE SkillKits
                SET Name = (SELECT DeduplicatedName FROM duplicates WHERE DuplicateRowid = SkillKits.rowid)
                WHERE rowid IN (SELECT DuplicateRowid FROM duplicates)
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SkillKits_Name",
                table: "SkillKits",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SkillKits_Name",
                table: "SkillKits");
        }
    }
}

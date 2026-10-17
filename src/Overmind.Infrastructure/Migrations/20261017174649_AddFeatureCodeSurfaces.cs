using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddFeatureCodeSurfaces : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CodeSurfaces",
            table: "PlanningFeatures",
            type: "TEXT",
            maxLength: 4000,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "PlanningFeatures" DROP COLUMN "CodeSurfaces";
            """);
    }
}

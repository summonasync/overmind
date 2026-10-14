using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddFeatureCodePin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PinnedBranch",
            table: "PlanningFeatures",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PinnedWorktreePath",
            table: "PlanningFeatures",
            type: "TEXT",
            maxLength: 400,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "PlanningFeatures" DROP COLUMN "PinnedBranch";
            ALTER TABLE "PlanningFeatures" DROP COLUMN "PinnedWorktreePath";
            """);
    }
}

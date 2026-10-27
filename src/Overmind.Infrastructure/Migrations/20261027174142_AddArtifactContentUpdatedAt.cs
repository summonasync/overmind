using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddArtifactContentUpdatedAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ContentUpdatedAt",
            table: "PlanningArtifacts",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.Sql(
            """
            UPDATE "PlanningArtifacts" SET "ContentUpdatedAt" = "UpdatedAt";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Raw SQL on purpose: the generated DropColumn rebuilds the table through a temp copy,
        // which silently drops the planning FTS triggers. Native DROP COLUMN keeps them in place.
        migrationBuilder.Sql(
            """
            ALTER TABLE "PlanningArtifacts" DROP COLUMN "ContentUpdatedAt";
            """);
    }
}

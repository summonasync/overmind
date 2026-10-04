using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAgentClaims : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Raw SQL on purpose: the generated DropColumn rebuilds the table through a temp copy,
        // which silently drops the planning FTS triggers and reassigns rowids. Native DROP COLUMN
        // keeps the table (and its triggers) in place.
        migrationBuilder.Sql(
            """ALTER TABLE "PlanningFeatures" DROP COLUMN "IsCurrent";""");

        migrationBuilder.AddColumn<string>(
            name: "AgentNamingRules",
            table: "Projects",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ClaimStaleThresholdMinutes",
            table: "Projects",
            type: "INTEGER",
            nullable: false,
            defaultValue: 15);

        migrationBuilder.AddColumn<Guid>(
            name: "ClaimSessionId",
            table: "PlanningFeatures",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ClaimedAt",
            table: "PlanningFeatures",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClaimedByAlias",
            table: "PlanningFeatures",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClaimedByVendor",
            table: "PlanningFeatures",
            type: "TEXT",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AgentSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Alias = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Vendor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentSessions", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AgentSessions");

        migrationBuilder.DropColumn(
            name: "AgentNamingRules",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "ClaimStaleThresholdMinutes",
            table: "Projects");

        migrationBuilder.Sql(
            """
            ALTER TABLE "PlanningFeatures" DROP COLUMN "ClaimSessionId";
            ALTER TABLE "PlanningFeatures" DROP COLUMN "ClaimedAt";
            ALTER TABLE "PlanningFeatures" DROP COLUMN "ClaimedByAlias";
            ALTER TABLE "PlanningFeatures" DROP COLUMN "ClaimedByVendor";
            """);

        migrationBuilder.AddColumn<bool>(
            name: "IsCurrent",
            table: "PlanningFeatures",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }
}

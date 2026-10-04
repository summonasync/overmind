using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPlanningFeatureDependencies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlanningFeatureDependencies",
            columns: table => new
            {
                FeatureId = table.Column<Guid>(type: "TEXT", nullable: false),
                BlockedByFeatureId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanningFeatureDependencies", x => new { x.FeatureId, x.BlockedByFeatureId });
                table.ForeignKey(
                    name: "FK_PlanningFeatureDependencies_PlanningFeatures_BlockedByFeatureId",
                    column: x => x.BlockedByFeatureId,
                    principalTable: "PlanningFeatures",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlanningFeatureDependencies_PlanningFeatures_FeatureId",
                    column: x => x.FeatureId,
                    principalTable: "PlanningFeatures",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningFeatureDependencies_BlockedByFeatureId",
            table: "PlanningFeatureDependencies",
            column: "BlockedByFeatureId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlanningFeatureDependencies");
    }
}

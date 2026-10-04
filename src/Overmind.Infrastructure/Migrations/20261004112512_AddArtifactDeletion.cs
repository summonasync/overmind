using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddArtifactDeletion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PlanningArtifacts_FeatureId_Kind_Order",
            table: "PlanningArtifacts");

        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "PlanningArtifacts",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeletedBy",
            table: "PlanningArtifacts",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlanningArtifacts_FeatureId_Kind_Order",
            table: "PlanningArtifacts",
            columns: new[] { "FeatureId", "Kind", "Order" },
            unique: true,
            filter: "\"DeletedAt\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PlanningArtifacts_FeatureId_Kind_Order",
            table: "PlanningArtifacts");

        // Raw SQL on purpose: the generated DropColumn rebuilds the table through a temp copy,
        // which silently drops the planning FTS triggers. Native DROP COLUMN keeps them in place.
        migrationBuilder.Sql(
            """
            ALTER TABLE "PlanningArtifacts" DROP COLUMN "DeletedAt";
            ALTER TABLE "PlanningArtifacts" DROP COLUMN "DeletedBy";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_PlanningArtifacts_FeatureId_Kind_Order",
            table: "PlanningArtifacts",
            columns: new[] { "FeatureId", "Kind", "Order" },
            unique: true);
    }
}

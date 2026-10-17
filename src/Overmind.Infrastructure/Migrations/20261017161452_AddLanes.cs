using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLanes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Lane",
            table: "PlanningFeatures",
            type: "TEXT",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PlanningLanes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                Contour = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                SerialOrder = table.Column<int>(type: "INTEGER", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanningLanes", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanningLanes_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningLanes_ProjectId_Name",
            table: "PlanningLanes",
            columns: new[] { "ProjectId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlanningLanes");

        migrationBuilder.Sql(
            """
            ALTER TABLE "PlanningFeatures" DROP COLUMN "Lane";
            """);
    }
}

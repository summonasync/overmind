using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPlanningOperationRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlanningOperationRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                RequestHash = table.Column<string>(type: "TEXT", nullable: false),
                StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                ResponseBody = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanningOperationRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanningOperationRecords_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningOperationRecords_ProjectId_OperationId",
            table: "PlanningOperationRecords",
            columns: new[] { "ProjectId", "OperationId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlanningOperationRecords");
    }
}

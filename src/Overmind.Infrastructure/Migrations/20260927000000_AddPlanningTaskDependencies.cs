using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningTaskDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanningTaskDependencies",
                columns: table => new
                {
                    BlockedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlockingId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningTaskDependencies", x => new { x.BlockedById, x.BlockingId });
                    table.ForeignKey(
                        name: "FK_PlanningTaskDependencies_PlanningArtifacts_BlockedById",
                        column: x => x.BlockedById,
                        principalTable: "PlanningArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningTaskDependencies_PlanningArtifacts_BlockingId",
                        column: x => x.BlockingId,
                        principalTable: "PlanningArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningTaskDependencies_BlockingId",
                table: "PlanningTaskDependencies",
                column: "BlockingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningTaskDependencies");
        }
    }
}

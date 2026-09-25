using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningFeatureQueuePosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QueuePosition",
                table: "PlanningFeatures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE PlanningFeatures
                SET QueuePosition = (
                    SELECT COUNT(*)
                    FROM PlanningFeatures AS f
                    WHERE f.ProjectId = PlanningFeatures.ProjectId
                      AND f.Priority = PlanningFeatures.Priority
                      AND (f.CreatedAt < PlanningFeatures.CreatedAt
                           OR (f.CreatedAt = PlanningFeatures.CreatedAt
                               AND (f.Slug < PlanningFeatures.Slug
                                    OR (f.Slug = PlanningFeatures.Slug
                                        AND f.Id <= PlanningFeatures.Id)))))
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QueuePosition",
                table: "PlanningFeatures");
        }
    }
}

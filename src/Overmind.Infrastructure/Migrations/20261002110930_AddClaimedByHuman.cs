using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimedByHuman : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClaimedByHuman",
                table: "PlanningFeatures",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        // Native DROP COLUMN like AddAgentClaims: the generated translation rebuilds the table
        // through a temp copy and silently drops the planning FTS triggers.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "PlanningFeatures" DROP COLUMN "ClaimedByHuman";
                """);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskIndependentFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Independent",
                table: "PlanningArtifacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Raw SQL on purpose: the generated DropColumn rebuilds the table through a temp
            // copy, which silently drops the planning FTS triggers living on PlanningArtifacts.
            migrationBuilder.Sql(
                """ALTER TABLE "PlanningArtifacts" DROP COLUMN "Independent";""");
        }
    }
}

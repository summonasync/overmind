using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddProjectRescuePolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RescuePolicy",
            table: "Projects",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE Projects
            SET RescuePolicy = 'everything unpushed may be pushed, everything unmerged I allow merging'
            WHERE RescuePolicy IS NULL
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RescuePolicy",
            table: "Projects");
    }
}

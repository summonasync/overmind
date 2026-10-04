using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddProjectLastMaterializationRun : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime?>(
            name: "LastMaterializationAttemptAt",
            table: "Projects",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastMaterializationRun",
            table: "Projects",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastMaterializationAttemptAt",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "LastMaterializationRun",
            table: "Projects");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddProjectLastMaterializationSkips : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "LastMaterializationSkipCount",
            table: "Projects",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastMaterializationSkipHead",
            table: "Projects",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastMaterializationSkipCount",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "LastMaterializationSkipHead",
            table: "Projects");
    }
}

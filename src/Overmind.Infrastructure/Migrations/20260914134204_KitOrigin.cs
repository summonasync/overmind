using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class KitOrigin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ImportedAtUtc",
            table: "SkillKits",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "SourceKind",
            table: "SkillKits",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "SourcePath",
            table: "SkillKits",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ImportedContentHash",
            table: "SkillKitEntries",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ImportedAtUtc",
            table: "SkillKits");

        migrationBuilder.DropColumn(
            name: "SourceKind",
            table: "SkillKits");

        migrationBuilder.DropColumn(
            name: "SourcePath",
            table: "SkillKits");

        migrationBuilder.DropColumn(
            name: "ImportedContentHash",
            table: "SkillKitEntries");
    }
}

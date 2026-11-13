using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enforces",
                table: "ContextEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "ContextEntries",
                type: "TEXT",
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<DateTime>(
                name: "StateAt",
                table: "ContextEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateBy",
                table: "ContextEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateProof",
                table: "ContextEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateReason",
                table: "ContextEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededById",
                table: "ContextEntries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Enforces",
                table: "ContextEntries");

            migrationBuilder.DropColumn(
                name: "State",
                table: "ContextEntries");

            migrationBuilder.DropColumn(
                name: "StateAt",
                table: "ContextEntries");

            migrationBuilder.DropColumn(
                name: "StateBy",
                table: "ContextEntries");

            migrationBuilder.DropColumn(
                name: "StateProof",
                table: "ContextEntries");

            migrationBuilder.DropColumn(
                name: "StateReason",
                table: "ContextEntries");

            migrationBuilder.DropColumn(
                name: "SupersededById",
                table: "ContextEntries");
        }
    }
}

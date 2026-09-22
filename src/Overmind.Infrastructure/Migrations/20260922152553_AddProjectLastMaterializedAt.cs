using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLastMaterializedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastMaterializedAt",
                table: "Projects",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMaterializedAt",
                table: "Projects");
        }
    }
}

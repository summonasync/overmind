using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaveRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorRole",
                table: "WaveMessages",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "worker");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AgentSessions",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "worker");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorRole",
                table: "WaveMessages");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AgentSessions");
        }
    }
}

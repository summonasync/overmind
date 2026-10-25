using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuesKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Issues",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "issue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Issues");
        }
    }
}

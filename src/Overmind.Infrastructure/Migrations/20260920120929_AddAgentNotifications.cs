using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAgentNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgentNotifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                FeatureSlug = table.Column<string>(type: "TEXT", nullable: true),
                Tab = table.Column<string>(type: "TEXT", nullable: true),
                Text = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                Agent = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                ReplyText = table.Column<string>(type: "TEXT", nullable: true),
                RepliedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentNotifications", x => x.Id);
                table.ForeignKey(
                    name: "FK_AgentNotifications_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentNotifications_ProjectId_CreatedAt",
            table: "AgentNotifications",
            columns: new[] { "ProjectId", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AgentNotifications");
    }
}

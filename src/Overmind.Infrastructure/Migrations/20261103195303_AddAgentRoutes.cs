using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TargetAgent = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    MissReason = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    HitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    LastHitAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRoutes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoutes_HitCount",
                table: "AgentRoutes",
                column: "HitCount");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoutes_Pattern",
                table: "AgentRoutes",
                column: "Pattern",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRoutes");
        }
    }
}

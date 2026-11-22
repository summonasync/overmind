using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalMaterialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalMarkerDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ContentSha256 = table.Column<string>(type: "TEXT", nullable: false),
                    DeployedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalMarkerDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalMarkerDeployments_SkillKits_KitId",
                        column: x => x.KitId,
                        principalTable: "SkillKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlobalMaterializationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportJson = table.Column<string>(type: "TEXT", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalMaterializationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalMaterializedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    MaterializedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalMaterializedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalMaterializedFiles_SkillKits_KitId",
                        column: x => x.KitId,
                        principalTable: "SkillKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalMarkerDeployments_KitId_Platform",
                table: "GlobalMarkerDeployments",
                columns: new[] { "KitId", "Platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalMaterializationRuns_Kind",
                table: "GlobalMaterializationRuns",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalMaterializedFiles_KitId_Platform",
                table: "GlobalMaterializedFiles",
                columns: new[] { "KitId", "Platform" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalMarkerDeployments");

            migrationBuilder.DropTable(
                name: "GlobalMaterializationRuns");

            migrationBuilder.DropTable(
                name: "GlobalMaterializedFiles");
        }
    }
}

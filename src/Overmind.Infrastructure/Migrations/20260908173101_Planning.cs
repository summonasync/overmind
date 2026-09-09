using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Planning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlanningMaterialization",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequireHumanValidation",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PlanningFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningFeatures_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeatureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Review = table.Column<int>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    FrozenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningArtifacts_PlanningFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "PlanningFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningMaterializedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeatureId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    MaterializedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningMaterializedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningMaterializedFiles_PlanningFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "PlanningFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningMaterializedFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanningArtifactVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningArtifactVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningArtifactVersions_PlanningArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "PlanningArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningArtifacts_FeatureId_Kind_Order",
                table: "PlanningArtifacts",
                columns: new[] { "FeatureId", "Kind", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningArtifactVersions_ArtifactId_Version",
                table: "PlanningArtifactVersions",
                columns: new[] { "ArtifactId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningFeatures_ProjectId_Slug",
                table: "PlanningFeatures",
                columns: new[] { "ProjectId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanningMaterializedFiles_FeatureId",
                table: "PlanningMaterializedFiles",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningMaterializedFiles_ProjectId_RelativePath",
                table: "PlanningMaterializedFiles",
                columns: new[] { "ProjectId", "RelativePath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningArtifactVersions");

            migrationBuilder.DropTable(
                name: "PlanningMaterializedFiles");

            migrationBuilder.DropTable(
                name: "PlanningArtifacts");

            migrationBuilder.DropTable(
                name: "PlanningFeatures");

            migrationBuilder.DropColumn(
                name: "PlanningMaterialization",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RequireHumanValidation",
                table: "Projects");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPlanningDirectives : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlanningDirectives",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PlanningFeatureId = table.Column<Guid>(type: "TEXT", nullable: false),
                Text = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                AuthorAlias = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                AuthorVendor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                AuthorSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                AckByAlias = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                AckVendor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                AckAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanningDirectives", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanningDirectives_PlanningFeatures_PlanningFeatureId",
                    column: x => x.PlanningFeatureId,
                    principalTable: "PlanningFeatures",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlanningDirectives_PlanningFeatureId_AckAt",
            table: "PlanningDirectives",
            columns: new[] { "PlanningFeatureId", "AckAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE "PlanningDirectives";
            """);
    }
}

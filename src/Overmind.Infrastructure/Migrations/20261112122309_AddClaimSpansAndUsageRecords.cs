using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimSpansAndUsageRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureClaimSpans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeatureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ByHuman = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureClaimSpans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureClaimSpans_PlanningFeatures_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "PlanningFeatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeatureClaimSpans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ts = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CacheReadTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CacheWriteTokens = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageRecords_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureClaimSpans_FeatureId_ReleasedAt",
                table: "FeatureClaimSpans",
                columns: new[] { "FeatureId", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureClaimSpans_ProjectId",
                table: "FeatureClaimSpans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_Alias_SessionId",
                table: "UsageRecords",
                columns: new[] { "Alias", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ProjectId",
                table: "UsageRecords",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_Ts",
                table: "UsageRecords",
                column: "Ts");

            migrationBuilder.Sql(
                """
                INSERT INTO "FeatureClaimSpans"
                    ("Id", "ProjectId", "FeatureId", "Alias", "Vendor", "SessionId", "ClaimedAt", "ReleasedAt", "ByHuman")
                SELECT upper(hex(randomblob(4))) || '-' || upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(2))) || '-' ||
                       upper(hex(randomblob(2))) || '-' || upper(hex(randomblob(6))),
                       "ProjectId", "Id", "ClaimedByAlias", "ClaimedByVendor", "ClaimSessionId",
                       COALESCE("ClaimedAt", strftime('%Y-%m-%d %H:%M:%f', 'now')),
                       NULL,
                       "ClaimedByHuman"
                FROM "PlanningFeatures"
                WHERE "ClaimSessionId" IS NOT NULL AND "ClaimedByAlias" IS NOT NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureClaimSpans");

            migrationBuilder.DropTable(
                name: "UsageRecords");
        }
    }
}

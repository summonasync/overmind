using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                DiskPath = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SkillKits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Version = table.Column<int>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SkillKits", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Tags",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tags", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ContextEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Title = table.Column<string>(type: "TEXT", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CurrentVersion = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContextEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContextEntries_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectKits",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                KitId = table.Column<Guid>(type: "TEXT", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectKits", x => new { x.ProjectId, x.KitId });
                table.ForeignKey(
                    name: "FK_ProjectKits_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectKits_SkillKits_KitId",
                    column: x => x.KitId,
                    principalTable: "SkillKits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EntryTags",
            columns: table => new
            {
                EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                TagId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EntryTags", x => new { x.EntryId, x.TagId });
                table.ForeignKey(
                    name: "FK_EntryTags_ContextEntries_EntryId",
                    column: x => x.EntryId,
                    principalTable: "ContextEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_EntryTags_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "EntryVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<int>(type: "INTEGER", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EntryVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_EntryVersions_ContextEntries_EntryId",
                    column: x => x.EntryId,
                    principalTable: "ContextEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SkillFiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                Platform = table.Column<int>(type: "INTEGER", nullable: false),
                RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SkillFiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_SkillFiles_ContextEntries_EntryId",
                    column: x => x.EntryId,
                    principalTable: "ContextEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SkillKitEntries",
            columns: table => new
            {
                KitId = table.Column<Guid>(type: "TEXT", nullable: false),
                EntryId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SkillKitEntries", x => new { x.KitId, x.EntryId });
                table.ForeignKey(
                    name: "FK_SkillKitEntries_ContextEntries_EntryId",
                    column: x => x.EntryId,
                    principalTable: "ContextEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_SkillKitEntries_SkillKits_KitId",
                    column: x => x.KitId,
                    principalTable: "SkillKits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ContextEntries_ProjectId",
            table: "ContextEntries",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_EntryTags_TagId",
            table: "EntryTags",
            column: "TagId");

        migrationBuilder.CreateIndex(
            name: "IX_EntryVersions_EntryId_Version",
            table: "EntryVersions",
            columns: new[] { "EntryId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectKits_KitId",
            table: "ProjectKits",
            column: "KitId");

        migrationBuilder.CreateIndex(
            name: "IX_SkillFiles_EntryId",
            table: "SkillFiles",
            column: "EntryId");

        migrationBuilder.CreateIndex(
            name: "IX_SkillKitEntries_EntryId",
            table: "SkillKitEntries",
            column: "EntryId");

        migrationBuilder.Sql(
            """
            CREATE VIRTUAL TABLE entry_fts USING fts5(title, content, content='', contentless_delete=1);

            CREATE TRIGGER trg_entry_fts_insert AFTER INSERT ON ContextEntries BEGIN
                INSERT INTO entry_fts(rowid, title, content) VALUES (NEW.rowid, NEW.Title, NEW.Content);
            END;

            CREATE TRIGGER trg_entry_fts_update AFTER UPDATE ON ContextEntries BEGIN
                UPDATE entry_fts SET title = NEW.Title, content = NEW.Content WHERE rowid = OLD.rowid;
            END;

            CREATE TRIGGER trg_entry_fts_delete AFTER DELETE ON ContextEntries BEGIN
                DELETE FROM entry_fts WHERE rowid = OLD.rowid;
            END;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS trg_entry_fts_delete;
            DROP TRIGGER IF EXISTS trg_entry_fts_update;
            DROP TRIGGER IF EXISTS trg_entry_fts_insert;
            DROP TABLE IF EXISTS entry_fts;
            """);

        migrationBuilder.DropTable(
            name: "EntryTags");

        migrationBuilder.DropTable(
            name: "EntryVersions");

        migrationBuilder.DropTable(
            name: "ProjectKits");

        migrationBuilder.DropTable(
            name: "SkillFiles");

        migrationBuilder.DropTable(
            name: "SkillKitEntries");

        migrationBuilder.DropTable(
            name: "Tags");

        migrationBuilder.DropTable(
            name: "ContextEntries");

        migrationBuilder.DropTable(
            name: "SkillKits");

        migrationBuilder.DropTable(
            name: "Projects");
    }
}

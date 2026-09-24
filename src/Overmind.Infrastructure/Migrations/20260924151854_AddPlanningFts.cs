using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE feature_fts USING fts5(title, body, content='', contentless_delete=1);
                CREATE VIRTUAL TABLE artifact_fts USING fts5(body, content='', contentless_delete=1);

                CREATE TRIGGER trg_feature_fts_insert AFTER INSERT ON PlanningFeatures BEGIN
                    INSERT INTO feature_fts(rowid, title, body) VALUES (NEW.rowid, NEW.Title, NEW.Body);
                END;

                CREATE TRIGGER trg_feature_fts_update AFTER UPDATE ON PlanningFeatures BEGIN
                    UPDATE feature_fts SET title = NEW.Title, body = NEW.Body WHERE rowid = OLD.rowid;
                END;

                CREATE TRIGGER trg_feature_fts_delete AFTER DELETE ON PlanningFeatures BEGIN
                    DELETE FROM feature_fts WHERE rowid = OLD.rowid;
                END;

                CREATE TRIGGER trg_artifact_fts_insert AFTER INSERT ON PlanningArtifacts BEGIN
                    INSERT INTO artifact_fts(rowid, body) VALUES (NEW.rowid, NEW.Body);
                END;

                CREATE TRIGGER trg_artifact_fts_update AFTER UPDATE ON PlanningArtifacts BEGIN
                    UPDATE artifact_fts SET body = NEW.Body WHERE rowid = OLD.rowid;
                END;

                CREATE TRIGGER trg_artifact_fts_delete AFTER DELETE ON PlanningArtifacts BEGIN
                    DELETE FROM artifact_fts WHERE rowid = OLD.rowid;
                END;

                INSERT INTO feature_fts(rowid, title, body) SELECT rowid, Title, Body FROM PlanningFeatures;
                INSERT INTO artifact_fts(rowid, body) SELECT rowid, Body FROM PlanningArtifacts;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_feature_fts_delete;
                DROP TRIGGER IF EXISTS trg_feature_fts_update;
                DROP TRIGGER IF EXISTS trg_feature_fts_insert;
                DROP TABLE IF EXISTS feature_fts;

                DROP TRIGGER IF EXISTS trg_artifact_fts_delete;
                DROP TRIGGER IF EXISTS trg_artifact_fts_update;
                DROP TRIGGER IF EXISTS trg_artifact_fts_insert;
                DROP TABLE IF EXISTS artifact_fts;
                """);
        }
    }
}

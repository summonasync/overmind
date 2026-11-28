using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeClaimSpanIdCasing : Migration
    {
        // The AddClaimSpansAndUsageRecords backfill generated lowercase GUID strings while EF binds
        // Guid parameters as UPPERCASE TEXT; every later UPDATE by such a PK matched 0 rows and blew
        // up as DbUpdateConcurrencyException (issue #9). Id carries no inbound FKs, rewriting is safe.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "FeatureClaimSpans" SET "Id" = upper("Id") WHERE "Id" <> upper("Id")
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

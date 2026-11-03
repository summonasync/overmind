using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Overmind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramValidatorMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramValidatorMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChatId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramValidatorMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramValidatorMappings_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramValidatorMappings_ChatId_ProjectId",
                table: "TelegramValidatorMappings",
                columns: new[] { "ChatId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramValidatorMappings_ConversationId",
                table: "TelegramValidatorMappings",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramValidatorMappings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accanto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CareCircleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Function = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InputJsonEncrypted = table.Column<string>(type: "text", nullable: false),
                    OutputEncrypted = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TookMs = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CacheHit = table.Column<bool>(type: "boolean", nullable: false),
                    Feedback = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    FeedbackAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_interactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_CareCircleId_CreatedAt",
                table: "ai_interactions",
                columns: new[] { "CareCircleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_interactions_UserId_CreatedAt",
                table: "ai_interactions",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_interactions");
        }
    }
}

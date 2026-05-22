using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accanto.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCareCircleInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "care_circle_invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CareCircleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaxUses = table.Column<int>(type: "integer", nullable: false),
                    UsedCount = table.Column<int>(type: "integer", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_circle_invites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_care_circle_invites_CareCircleId",
                table: "care_circle_invites",
                column: "CareCircleId");

            migrationBuilder.CreateIndex(
                name: "IX_care_circle_invites_Token",
                table: "care_circle_invites",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "care_circle_invites");
        }
    }
}
